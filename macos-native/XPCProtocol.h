//
//  onionfruitxpc.h
//  onionfruitd
//
//  Created by Albie on 01/06/2025.
//

#import <Foundation/Foundation.h>

#define RESULT_OK 0
#define RESULT_NETSERVICE_NOT_FOUND 1
#define RESULT_NETSERVICE_UNSUPPORTED_PROTOCOL 2
#define RESULT_CONFIG_UPDATE_FAILED 3
#define RESULT_XPC_CONNECTION_FAILED 4
#define RESULT_XPC_TIMEOUT 5
#define RESULT_XPC_VERSION_MISMATCH 6

#define XPC_PROTOCOL_VERSION 1

@protocol OnionFruitXPCProtocol

- (void) xpcServerVersion: (void (^)(NSNumber*)) reply;

- (void) getServiceConfig: (NSString*) netServiceId
              forProtocol: (NSString*) protocolType
                withReply: (void (^)(NSNumber*, NSDictionary*)) reply;

- (void) setServiceConfig: (NSString*) netServiceId
              forProtocol: (NSString*) protocolType
               withConfig: (NSDictionary*) config
                withReply: (void (^)(NSNumber*)) reply;

@end
