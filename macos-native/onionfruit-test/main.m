//
//  main.m
//  onionfruit-test
//
//  Created by Albie on 02/06/2025.
//

#import "XPCBridge.h"
#import "onionfruitd.h"

#import <Foundation/Foundation.h>
#import <SystemConfiguration/SystemConfiguration.h>

//@interface ServiceDelegate : NSObject<NSXPCListenerDelegate>
//@end
//
//@implementation ServiceDelegate
//
//- (BOOL)listener:(NSXPCListener*)listener shouldAcceptNewConnection:(NSXPCConnection*)newConnection {
//    newConnection.exportedInterface = [NSXPCInterface interfaceWithProtocol: @protocol(OnionFruitXPCProtocol)];
//    newConnection.exportedObject = [OnionFruitXPC new];
//
//    [newConnection resume];
//    return YES;
//}
//
//@end

int main(int argc, const char* argv[]) {
    NSString* nullBridgedString = (__bridge NSString*)NULL;
    
    strdup([nullBridgedString UTF8String]);
    
//    ServiceDelegate* delegate = [ServiceDelegate new];
//    NSXPCListener* listener = [NSXPCListener anonymousListener];
//
//    listener.delegate = delegate;
//    [listener resume];
//
//    NSLog(@"OnionFruitXPC service started");
//
//    NSXPCConnection* connection = [[NSXPCConnection alloc] initWithListenerEndpoint: listener.endpoint];
//    connection.remoteObjectInterface = [NSXPCInterface interfaceWithProtocol: @protocol(OnionFruitXPCProtocol)];
//    [connection resume];
//
//    id proxy = [connection synchronousRemoteObjectProxyWithErrorHandler: ^(NSError* error) {
//        NSLog(@"Error connecting to OnionFruitXPC service: %@", error);
//    }];
//
//    [proxy xpcServerVersion: ^(NSNumber* version) {
//        NSLog(@"OnionFruitXPC service version: %@", version);
//    }];
//
//    [connection invalidate];
//    [listener invalidate];
}
