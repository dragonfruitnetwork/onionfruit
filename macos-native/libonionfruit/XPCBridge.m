//
//  XPCBridge.m
//  libonionfruit-macos
//
//  Created by Albie on 03/06/2025.
//

#import "XPCBridge.h"
#import "XPCProtocol.h"

#import <Foundation/Foundation.h>
#import <SystemConfiguration/SystemConfiguration.h>


char* cloneDictStringValue(NSDictionary* dict, NSString* key);
void processProxyHost(char* strValue, NSMutableDictionary* configDict, NSString* configKey, NSString* portKey);


int32_t createXpcConnection(const char* serviceName, CFTypeRef* connectionPtr, int32_t* serverVersion) {
    NSXPCConnection* connection = [[NSXPCConnection alloc] initWithMachServiceName: [NSString stringWithUTF8String: serviceName] options: NSXPCConnectionPrivileged];
    connection.remoteObjectInterface = [NSXPCInterface interfaceWithProtocol: @protocol(OnionFruitXPCProtocol)];

    [connection resume];

    __block dispatch_semaphore_t semaphore = dispatch_semaphore_create(0);
    __block int reportedServerVersion = 0;

    id proxy = [connection remoteObjectProxyWithErrorHandler: ^(NSError* error) {
        dispatch_semaphore_signal(semaphore);
    }];

    [proxy xpcServerVersion: ^(NSNumber* version) {
        reportedServerVersion = [version intValue];
        dispatch_semaphore_signal(semaphore);
    }];

    bool timedOut = dispatch_semaphore_wait(semaphore, dispatch_time(DISPATCH_TIME_NOW, 5 * NSEC_PER_SEC)) != 0;

    if (!timedOut && reportedServerVersion == XPC_PROTOCOL_VERSION) {
        *connectionPtr = CFBridgingRetain(connection);
        *serverVersion = reportedServerVersion;
        
        return RESULT_OK;
    }

    *serverVersion = -1;
    *connectionPtr = NULL;

    [connection invalidate];

    if (timedOut) {
        return RESULT_XPC_TIMEOUT;
    } else if (reportedServerVersion) {
        return RESULT_XPC_VERSION_MISMATCH;
    }

    return RESULT_XPC_CONNECTION_FAILED;
}

void destroyXpcConnection(CFTypeRef xpcConnection) {
    if (xpcConnection == NULL) {
        return;
    }

    NSXPCConnection* connection = (__bridge_transfer NSXPCConnection*)xpcConnection;
    [connection invalidate];
}

int32_t getServiceProxyConfig(CFTypeRef xpcConnection, const char* netServiceId, ServiceProxyConfig** proxyConfig) {
    NSXPCConnection* connection = (__bridge NSXPCConnection*)xpcConnection;

    __block dispatch_semaphore_t semaphore = dispatch_semaphore_create(0);
    __block ServiceProxyConfig* config = NULL;
    __block int errorCode = RESULT_OK;

    id proxy = [connection remoteObjectProxyWithErrorHandler: ^(NSError* error) {
        errorCode = RESULT_XPC_CONNECTION_FAILED;
        dispatch_semaphore_signal(semaphore);
    }];

    [proxy getServiceConfig: [NSString stringWithUTF8String: netServiceId] forProtocol: (__bridge NSString*)kSCNetworkProtocolTypeProxies withReply: ^(NSNumber* status, NSDictionary* proxyConfigDict) {
        if ([status isEqual: @RESULT_OK]) {
            config = calloc(1, sizeof(ServiceProxyConfig));
            
            config->socksProxyEnabled = [([proxyConfigDict valueForKey: (__bridge NSString*)kSCPropNetProxiesSOCKSEnable] ?: @NO) unsignedCharValue];
            config->socksProxyHost = cloneDictStringValue(proxyConfigDict, (__bridge NSString*)kSCPropNetProxiesSOCKSProxy);
            config->socksProxyPort = [([proxyConfigDict valueForKey: (__bridge NSString*)kSCPropNetProxiesSOCKSPort] ?: @0) unsignedShortValue];
            
            config->httpProxyEnabled = [([proxyConfigDict valueForKey: (__bridge NSString*)kSCPropNetProxiesHTTPEnable] ?: @NO) unsignedCharValue];
            config->httpProxyHost = cloneDictStringValue(proxyConfigDict, (__bridge NSString*)kSCPropNetProxiesHTTPProxy);
            config->httpProxyPort = [([proxyConfigDict valueForKey: (__bridge NSString*)kSCPropNetProxiesHTTPPort] ?: @0) unsignedShortValue];
            
            config->httpsProxyEnabled = [([proxyConfigDict valueForKey: (__bridge NSString*)kSCPropNetProxiesHTTPSEnable] ?: @NO) unsignedCharValue];
            config->httpsProxyHost = cloneDictStringValue(proxyConfigDict, (__bridge NSString*)kSCPropNetProxiesHTTPSProxy);
            config->httpsProxyPort = [([proxyConfigDict valueForKey: (__bridge NSString*)kSCPropNetProxiesHTTPSPort] ?: @0) unsignedShortValue];
            
            config->autoDiscoveryEnabled = [([proxyConfigDict valueForKey: (__bridge NSString*)kSCPropNetProxiesProxyAutoDiscoveryEnable] ?: @NO) boolValue];
            config->autoConfigurationUrl = cloneDictStringValue(proxyConfigDict, (__bridge NSString*)kSCPropNetProxiesProxyAutoConfigURLString);
        }

        errorCode = [status intValue];
        dispatch_semaphore_signal(semaphore);
    }];

    dispatch_semaphore_wait(semaphore, DISPATCH_TIME_FOREVER);

    *proxyConfig = config;

    return errorCode;
}

void destroyServiceProxyConfig(ServiceProxyConfig* config) {
    if (config == NULL) {
        return;
    }

    char** stringFields[] = {
        &config->socksProxyHost,
        &config->httpProxyHost,
        &config->httpsProxyHost,
        &config->autoConfigurationUrl
    };

    size_t count = sizeof(stringFields) / sizeof(stringFields[0]);

    for (size_t i = 0; i < count; i++) {
        if (*stringFields[i] == NULL) {
            continue;
        }
        
        free(*stringFields[i]);
        *stringFields[i] = NULL;
    }

    free(config);
}

int32_t getServiceDnsResolvers(CFTypeRef xpcConnection, const char* netServiceId, char*** resolverList, int32_t* resolverCount) {
    NSXPCConnection* connection = (__bridge NSXPCConnection*)xpcConnection;

    __block dispatch_semaphore_t semaphore = dispatch_semaphore_create(0);
    __block int errorCode = RESULT_OK;

    __block char** resolverArr = NULL;
    __block int resolverArrLen = 0;

    id proxy = [connection remoteObjectProxyWithErrorHandler: ^(NSError* error) {
        errorCode = RESULT_XPC_CONNECTION_FAILED;
        dispatch_semaphore_signal(semaphore);
    }];

    [proxy getServiceConfig: [NSString stringWithUTF8String: netServiceId] forProtocol: (__bridge NSString*)kSCNetworkProtocolTypeDNS withReply: ^(NSNumber* status, NSDictionary* dnsConfigDict) {
        if ([status isEqual: @RESULT_OK]) {
            for (id entry in [dnsConfigDict valueForKey: (__bridge NSString*)kSCPropNetDNSServerAddresses]) {
                if (![entry isKindOfClass: [NSString class]]) {
                    continue;
                }
                
                resolverArr = realloc(resolverArr, (resolverArrLen + 1) * sizeof(char*));
                resolverArr[resolverArrLen++] = strdup([entry UTF8String]);
            }
            
            // NULL-terminate array
            resolverArr = realloc(resolverArr, (resolverArrLen + 1) * sizeof(char*));
            resolverArr[resolverArrLen] = NULL;
        }

        errorCode = [status intValue];
        dispatch_semaphore_signal(semaphore);
    }];

    dispatch_semaphore_wait(semaphore, DISPATCH_TIME_FOREVER);

    *resolverList = resolverArr;
    *resolverCount = resolverArrLen;

    return errorCode;
}

void destroyServiceDnsResolvers(char** resolvers) {
    if (resolvers == NULL) {
        return;
    }

    char* currentPtr = *resolvers;

    do {
        free(currentPtr);
        currentPtr += sizeof(char*);
    } while (currentPtr != NULL);

    free(resolvers);
}

int32_t setServiceDnsResolvers(CFTypeRef xpcConnection, const char* netServiceId, char** resolvers, int32_t resolverCount) {
    NSXPCConnection* connection = (__bridge NSXPCConnection*)xpcConnection;

    __block int errorCode = 0;

    id proxy = [connection synchronousRemoteObjectProxyWithErrorHandler: ^(NSError* error) {
        errorCode = RESULT_XPC_CONNECTION_FAILED;
    }];

    __block NSString* serviceId = [NSString stringWithUTF8String: netServiceId];
    __block NSString* protocolType = (__bridge NSString*)kSCNetworkProtocolTypeDNS;

    __block NSMutableDictionary* newConfig;

    // because we're not setting an entire configuration, get the existing one and apply over it
    [proxy getServiceConfig: serviceId forProtocol: protocolType withReply: ^(NSNumber* status, NSDictionary* currentConfig) {
        if ([status isEqual:@RESULT_OK]) {
            newConfig = [currentConfig mutableCopy] ?: [NSMutableDictionary dictionary];
        }
        
        errorCode = [status intValue];
    }];

    if (errorCode) {
        return errorCode;
    }

    if (resolverCount) {
        NSMutableArray* dnsServers = [NSMutableArray arrayWithCapacity: resolverCount];

        for (int32_t i = 0; i < resolverCount; i++) {
            [dnsServers addObject:[NSString stringWithUTF8String: resolvers[i]]];
        }
        
        [newConfig setObject: [NSArray arrayWithArray: dnsServers] forKey: (__bridge NSString*)kSCPropNetDNSServerAddresses];
    } else {
        [newConfig removeObjectForKey: (__bridge NSString*)kSCPropNetDNSServerAddresses];
    }

    [proxy setServiceConfig: serviceId forProtocol: protocolType withConfig: newConfig withReply: ^(NSNumber* status) {
        errorCode = [status intValue];
    }];

    return errorCode;
}

int32_t setServiceProxyConfig(CFTypeRef xpcConnection, const char* netServiceId, ServiceProxyConfig config) {
    NSXPCConnection* connection = (__bridge NSXPCConnection*)xpcConnection;

    __block int errorCode = 0;
    __block NSMutableDictionary* newConfig = nil;

    id serviceId = [NSString stringWithUTF8String: netServiceId];
    id protocolType = (__bridge NSString*)kSCNetworkProtocolTypeProxies;

    id proxy = [connection synchronousRemoteObjectProxyWithErrorHandler: ^(NSError* error) {
        errorCode = RESULT_XPC_CONNECTION_FAILED;
    }];

    [proxy getServiceConfig: serviceId forProtocol: protocolType withReply: ^(NSNumber* status, NSDictionary* currentConfig) {
        if ([status isEqual: @RESULT_OK]) {
            newConfig = [currentConfig mutableCopy] ?: [NSMutableDictionary dictionary];
        }
        
        errorCode = [status intValue];
    }];

    if (errorCode || !newConfig) {
        return errorCode;
    }

    // process http/https/socks proxy settings (enabled/port/host)
    [newConfig setValue: [NSNumber numberWithInt: MIN(1, config.httpProxyEnabled)] forKey: (__bridge NSString*)kSCPropNetProxiesHTTPEnable];
    [newConfig setValue: [NSNumber numberWithInt: MIN(1, config.httpsProxyEnabled)] forKey: (__bridge NSString*)kSCPropNetProxiesHTTPSEnable];
    [newConfig setValue: [NSNumber numberWithInt: MIN(1, config.socksProxyEnabled)] forKey: (__bridge NSString*)kSCPropNetProxiesSOCKSEnable];

    [newConfig setValue: [NSNumber numberWithInt: config.httpProxyPort] forKey: (__bridge NSString*)kSCPropNetProxiesHTTPPort];
    [newConfig setValue: [NSNumber numberWithInt: config.httpsProxyPort] forKey: (__bridge NSString*)kSCPropNetProxiesHTTPSPort];
    [newConfig setValue: [NSNumber numberWithInt: config.socksProxyPort] forKey: (__bridge NSString*)kSCPropNetProxiesSOCKSPort];

    // note: previously set ports get wiped out if the host was not set
    processProxyHost(config.httpProxyHost, newConfig, (__bridge NSString*)kSCPropNetProxiesHTTPProxy, (__bridge NSString*)kSCPropNetProxiesHTTPPort);
    processProxyHost(config.httpsProxyHost, newConfig, (__bridge NSString*)kSCPropNetProxiesHTTPSProxy, (__bridge NSString*)kSCPropNetProxiesHTTPSPort);
    processProxyHost(config.socksProxyHost, newConfig, (__bridge NSString*)kSCPropNetProxiesSOCKSProxy, (__bridge NSString*)kSCPropNetProxiesSOCKSPort);

    // autodiscovery
    [newConfig setValue: [NSNumber numberWithInt: MIN(1, config.autoDiscoveryEnabled)] forKey: (__bridge NSString*)kSCPropNetProxiesProxyAutoDiscoveryEnable];

    // autoconfiguration url
    processProxyHost(config.autoConfigurationUrl, newConfig, (__bridge NSString*)kSCPropNetProxiesProxyAutoConfigURLString, nil);

    [proxy setServiceConfig: serviceId forProtocol: protocolType withConfig: newConfig withReply: ^(NSNumber* status) {
        errorCode = [status intValue];
    }];

    return errorCode;
}

// utility functions
char* cloneDictStringValue(NSDictionary* dict, NSString* key) {
    NSString* value = [dict valueForKey: key];

    if (value != nil) {
        return strdup([value UTF8String]);
    }

    return NULL;
}

void processProxyHost(char* strValue, NSMutableDictionary* configDict, NSString* configKey, NSString* portKey) {
    NSString* value = strValue != NULL ? [NSString stringWithUTF8String: strValue] : nil;

    if (value) {
        [configDict setObject: value forKey: configKey];
    } else {
        [configDict removeObjectForKey: configKey];
        
        if (portKey) {
            [configDict removeObjectForKey: portKey];
        }
    }
}
