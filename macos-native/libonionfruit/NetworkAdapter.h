//
//  NetworkAdapter.h
//  libonionfruit-macos
//
//  Created by Albie on 02/06/2025.
//

#import <Foundation/Foundation.h>

#ifndef adapter_h
#define adapter_h

typedef struct onionfruit_network_service {
    char* serviceId;
    char* interfaceId;
    char* friendlyName;
} networkServiceInfo __attribute__ ((aligned (1)));

FOUNDATION_EXPORT networkServiceInfo* createNetworkServiceList(int32_t* count);
FOUNDATION_EXPORT void destroyNetworkServiceList(networkServiceInfo* list, int32_t count);

#endif /* adapter_h */
