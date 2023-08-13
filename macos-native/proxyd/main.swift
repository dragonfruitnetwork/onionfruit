//
//  main.swift
//  proxyd
//
//  Created by Albie Spriddell on 11/08/2023.
//

import Foundation

class ServiceDelegate : NSObject, NSXPCListenerDelegate {
    func listener(_ listener: NSXPCListener, shouldAcceptNewConnection newConnection: NSXPCConnection) -> Bool {
        let exportedObject = RpcServer()
        
        newConnection.exportedInterface = NSXPCInterface(with: RpcProtocol.self)
        newConnection.exportedObject = exportedObject
        
        newConnection.resume()
        return true
    }
}

let delegate = ServiceDelegate()
let listener = NSXPCListener(machServiceName: "network.dragonfruit.proxyd")

listener.delegate = delegate;
listener.resume()

RunLoop.main.run()
