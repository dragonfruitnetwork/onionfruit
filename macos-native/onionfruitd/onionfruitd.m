//
//  onionfruitd.m
//  onionfruitd
//
//  Created by Albie on 01/06/2025.
//

#import "onionfruitd.h"
#import <SystemConfiguration/SystemConfiguration.h>

@implementation OnionFruitXPC

- (void) xpcServerVersion: (void (^)(NSNumber*))reply {
    reply(@XPC_PROTOCOL_VERSION);
}

- (void) getServiceConfig: (NSString*)netServiceId forProtocol: (NSString*)protocolType withReply: (void (^)(NSNumber*, NSDictionary*))reply {
    SCPreferencesRef prefRef = SCPreferencesCreate(NULL, CFSTR("onionfruitd"), NULL);
    SCPreferencesLock(prefRef, true);

    SCNetworkServiceRef currentServiceRef = SCNetworkServiceCopy(prefRef, (__bridge CFStringRef)netServiceId);

    if (currentServiceRef == NULL) {
        SCPreferencesUnlock(prefRef);
        
        reply(@RESULT_NETSERVICE_NOT_FOUND, nil);
        
        CFRelease(prefRef);
        return;
    }

    CFArrayRef supportedProtocols = SCNetworkInterfaceGetSupportedProtocolTypes(SCNetworkServiceGetInterface(currentServiceRef));
    NSInteger protocolFound = [(__bridge NSArray*)supportedProtocols indexOfObject: protocolType];

    if (protocolFound == NSNotFound) {
        SCPreferencesUnlock(prefRef);
        
        reply(@RESULT_NETSERVICE_UNSUPPORTED_PROTOCOL, nil);

        CFRelease(currentServiceRef);
        CFRelease(prefRef);
        return;
    }

    SCNetworkProtocolRef serviceProtocolRef = SCNetworkServiceCopyProtocol(currentServiceRef, CFArrayGetValueAtIndex(supportedProtocols, protocolFound));
    CFDictionaryRef serviceDictionary = SCNetworkProtocolGetConfiguration(serviceProtocolRef);
    NSDictionary* dictionaryClone = [(__bridge NSDictionary*)serviceDictionary copy];

    SCPreferencesUnlock(prefRef);

    CFRelease(serviceProtocolRef);
    CFRelease(currentServiceRef);
    CFRelease(prefRef);

    if (serviceDictionary) {
        reply(@RESULT_OK, dictionaryClone);
    } else {
        reply(@RESULT_NETSERVICE_UNSUPPORTED_PROTOCOL, nil);
    }
}

- (void) setServiceConfig: (NSString*)netServiceId forProtocol: (NSString*)protocolType withConfig: (NSDictionary*)config withReply: (void (^)(NSNumber*))reply {
    SCPreferencesRef prefRef = SCPreferencesCreate(NULL, CFSTR("onionfruitd"), NULL);
    SCPreferencesLock(prefRef, true);

    SCNetworkServiceRef currentServiceRef = SCNetworkServiceCopy(prefRef, (__bridge CFStringRef)netServiceId);

    if (currentServiceRef == NULL) {
        SCPreferencesUnlock(prefRef);
        
        reply(@RESULT_NETSERVICE_NOT_FOUND);
        
        CFRelease(prefRef);
        return;
    }

    CFArrayRef supportedProtocols = SCNetworkInterfaceGetSupportedProtocolTypes(SCNetworkServiceGetInterface(currentServiceRef));
    NSInteger protocolFound = [(__bridge NSArray*)supportedProtocols indexOfObject: protocolType];

    if (protocolFound == NSNotFound) {
        SCPreferencesUnlock(prefRef);
        
        reply(@RESULT_NETSERVICE_UNSUPPORTED_PROTOCOL);

        CFRelease(currentServiceRef);
        CFRelease(prefRef);
        return;
    }

    SCNetworkProtocolRef serviceProtocolRef = SCNetworkServiceCopyProtocol(currentServiceRef, CFArrayGetValueAtIndex(supportedProtocols, protocolFound));

    bool setSuccess = SCNetworkProtocolSetConfiguration(serviceProtocolRef, (__bridge CFDictionaryRef)config);
    bool commitSuccess = SCPreferencesCommitChanges(prefRef);
    bool applicationSuccess = SCPreferencesApplyChanges(prefRef);

    SCPreferencesUnlock(prefRef);

    CFRelease(serviceProtocolRef);
    CFRelease(currentServiceRef);
    CFRelease(prefRef);

    if (!setSuccess || !commitSuccess || !applicationSuccess) {
        reply(@RESULT_CONFIG_UPDATE_FAILED);
    } else {
        reply(@RESULT_OK);
    }
}

@end
