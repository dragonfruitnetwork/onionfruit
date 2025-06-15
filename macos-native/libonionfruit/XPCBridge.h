//
//  XPCBridge.h
//  libonionfruit-macos
//
//  Created by Albie on 03/06/2025.
//

#import <Foundation/Foundation.h>
#import <CoreFoundation/CoreFoundation.h>

#ifndef xpc_h
#define xpc_h

typedef struct onionfruit_xpc_netservice_proxy_config {
    char* httpProxyHost;
    uint16_t httpProxyPort;
    uint8_t httpProxyEnabled;

    char* httpsProxyHost;
    uint16_t httpsProxyPort;
    uint8_t httpsProxyEnabled;

    char* socksProxyHost;
    uint16_t socksProxyPort;
    uint8_t socksProxyEnabled;

    uint8_t autoDiscoveryEnabled;
    char* autoConfigurationUrl;
} ServiceProxyConfig __attribute__ ((aligned (8)));

FOUNDATION_EXPORT int32_t createXpcConnection(const char* serviceName, CFTypeRef* connectionPtr, int32_t* serverVersion);
FOUNDATION_EXPORT int32_t checkXpcConnection(CFTypeRef xpcConnection);
FOUNDATION_EXPORT void destroyXpcConnection(CFTypeRef xpcConnection);

FOUNDATION_EXPORT int32_t getServiceProxyConfig(CFTypeRef xpcConnection, const char* netServiceId, ServiceProxyConfig** proxyConfig);
FOUNDATION_EXPORT void destroyServiceProxyConfig(ServiceProxyConfig* config);

FOUNDATION_EXPORT int32_t getServiceDnsResolvers(CFTypeRef xpcConnection, const char* netServiceId, char*** resolverList, int32_t* resolverCount);
FOUNDATION_EXPORT void destroyServiceDnsResolvers(char** resolvers);

FOUNDATION_EXPORT int32_t setServiceDnsResolvers(CFTypeRef xpcConnection, const char* netServiceId, char** resolvers, int32_t resolverCount);
FOUNDATION_EXPORT int32_t setServiceProxyConfig(CFTypeRef xpcConnection, const char* netServiceId, ServiceProxyConfig config);

#endif /* xpc_h */
