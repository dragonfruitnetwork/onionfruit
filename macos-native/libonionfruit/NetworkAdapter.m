//
//  NetworkAdapter.m
//  libonionfruit-macos
//
//  Created by Albie on 02/06/2025.
//

#import "NetworkAdapter.h"

#import <Foundation/Foundation.h>
#import <SystemConfiguration/SystemConfiguration.h>

networkServiceInfo* createNetworkServiceList(int32_t* count) {
    id preferences = (__bridge_transfer NSObject*)SCPreferencesCreate(NULL, CFSTR("onionfruit"), NULL);
    id networkServices = (__bridge_transfer NSArray*)SCNetworkServiceCopyAll((__bridge SCPreferencesRef)preferences);

    int32_t serviceCount = 0;
    networkServiceInfo* serviceList = NULL;

    for (id service in networkServices) {
        id name = (__bridge NSString*)SCNetworkServiceGetName((SCNetworkServiceRef)service);
        id serviceId = (__bridge NSString*)SCNetworkServiceGetServiceID((SCNetworkServiceRef)service);
        id interfaceId = (__bridge NSString*)SCNetworkInterfaceGetBSDName(SCNetworkServiceGetInterface((SCNetworkServiceRef)service));
        
        if (name == nil || serviceId == nil || interfaceId == nil) {
            continue;
        }

        char* cName = strdup([name UTF8String]);
        char* cServiceId = strdup([serviceId UTF8String]);
        char* cInterfaceId = strdup([interfaceId UTF8String]);

        serviceList = realloc(serviceList, (serviceCount + 1) * sizeof(networkServiceInfo));
        serviceList[serviceCount++] = (networkServiceInfo) {cServiceId, cInterfaceId, cName};
    }

    *count = serviceCount;
    return serviceList;
}

void destroyNetworkServiceList(networkServiceInfo* list, int32_t count) {
    if (list == NULL) {
        return;
    }

    for (int32_t i = 0; i < count; i++) {
        networkServiceInfo adapter = list[i];
        
        if (adapter.friendlyName != NULL) {
            free(adapter.friendlyName);
        }
        
        if (adapter.interfaceId != NULL) {
            free(adapter.interfaceId);
        }
        
        if (adapter.serviceId != NULL) {
            free(adapter.serviceId);
        }
    }

    free(list);
}
