//
//  main.m
//  onionfruitd
//
//  Created by Albie on 03/06/2025.
//

#import "onionfruitd.h"
#import <Foundation/Foundation.h>

@interface ServiceDelegate : NSObject<NSXPCListenerDelegate>
@end

@implementation ServiceDelegate

- (BOOL) listener: (NSXPCListener*)listener shouldAcceptNewConnection: (NSXPCConnection*)newConnection {
    newConnection.exportedInterface = [NSXPCInterface interfaceWithProtocol: @protocol(OnionFruitXPCProtocol)];
    newConnection.exportedObject = [OnionFruitXPC new];

#if (!DEV_MODE)
    [newConnection setCodeSigningRequirement:
     @"identifier \"network.dragonfruit.onionfruit\""
     " and anchor apple generic"
     " and certificate 1[field.1.2.840.113635.100.6.2.6]" /* exists */
     " and certificate leaf[field.1.2.840.113635.100.6.1.13]" /* exists */
     " and certificate leaf[subject.OU] = Q824VHAT9S"
     " and !entitlement[\"com.apple.security.get-task-allow\"]"
     " and !entitlement[\"com.apple.security.cs.disable-library-validation\"]"
     " and !entitlement[\"com.apple.security.cs.allow-dyld-environment-variables\"]"
    ];
#endif

    [newConnection resume];
    return YES;
}

@end

int main(int argc, const char* argv[]) {
    ServiceDelegate* delegate = [ServiceDelegate new];

#if (DEV_MODE)
    NSString* serviceName = @"network.dragonfruit.onionfruit.xpc-dev";
#else
    NSString* serviceName = @"network.dragonfruit.onionfruit.xpc";
#endif

    NSXPCListener* listener = [[NSXPCListener alloc] initWithMachServiceName: serviceName];

    listener.delegate = delegate;

    [listener resume];
    [NSRunLoop.mainRunLoop run];
}
