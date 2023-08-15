//
//  rpcServer.swift
//  proxyd
//
//  Created by Albie Spriddell on 11/08/2023.
//

import Foundation
import SystemConfiguration

let helperProcessName = "OnionFruit Native Services" as CFString

@objc class RpcServer: NSObject, RpcProtocol {
    
    func getVersion(reply: @escaping (UInt16) -> Void) {
        reply(1)
    }
    
    func getUid(reply: @escaping (String) -> Void) {
        reply(NSUserName())
    }
    
    func setProxy(_ url: String, reply: @escaping (Bool) -> Void) {
        // load in config
        let systemPreferences = SCPreferencesCreate(kCFAllocatorDefault, helperProcessName, nil)!
        var proxyCollection: Dictionary<NSObject, AnyObject> = [
            kCFNetworkProxiesExcludeSimpleHostnames: 1 as NSNumber
        ]
        
        for proxyUrl in url.split(separator: ",") {
            _ = NetworkInterface.parseProxyUrl(url: proxyUrl, collection: &proxyCollection)
        }
        
        NetworkInterface.applyProxySettings(preferences: systemPreferences, configuration: proxyCollection as CFDictionary)
        reply(true)
    }
    
    func clearProxies(reply: @escaping (Bool) -> Void) {
        let systemPreferences = SCPreferencesCreate(kCFAllocatorDefault, helperProcessName, nil)!
        var proxyCollection = Dictionary<NSObject, AnyObject>();
        
        for key in [kCFNetworkProxiesHTTPEnable, kCFNetworkProxiesHTTPSEnable, kCFNetworkProxiesSOCKSEnable] {
            proxyCollection[key] = 0 as NSNumber
        }
        
        NetworkInterface.applyProxySettings(preferences: systemPreferences, configuration: proxyCollection as CFDictionary)
        reply(true)
    }
}
