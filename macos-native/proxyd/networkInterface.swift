//
//  networkInterface.swift
//  proxyd
//
//  Created by Albie Spriddell on 11/08/2023.
//

import Foundation
import SystemConfiguration

class NetworkInterface {
    private static var proxyStringPattern = /(socks|http|https)=([\da-f:.]{3,128}):(\d{1,4})/.ignoresCase()

    /// Parses a windows-style proxy url and sets the corresponding settings in the provided configuration dictionary.
    /// Currently supports socks, http and https
    /// - Parameters:
    ///   - url: The windows-style URL to parse (socks=127.0.0.1:9050)
    ///   - collection: The collection to set the parsed configuration on
    /// - Returns: Whether the operation was a success
    internal static func parseProxyUrl(url: Substring, collection: inout Dictionary<NSObject, AnyObject>) -> Bool {
        if let proxyUrlSegments = url.wholeMatch(of: proxyStringPattern) {
            switch(proxyUrlSegments.1) {
                case "socks":
                    collection[kCFNetworkProxiesSOCKSEnable] = 1 as NSNumber
                    collection[kCFNetworkProxiesSOCKSProxy] = proxyUrlSegments.2 as AnyObject?
                    collection[kCFNetworkProxiesSOCKSPort] = NSNumber(value: UInt16(proxyUrlSegments.3)!)
                    return true
                
                case "http":
                    collection[kCFNetworkProxiesHTTPEnable] = 1 as NSNumber
                    collection[kCFNetworkProxiesHTTPProxy] = proxyUrlSegments.2 as AnyObject?
                    collection[kCFNetworkProxiesHTTPPort] = NSNumber(value: UInt16(proxyUrlSegments.3)!)
                    return true
                
                case "https":
                    collection[kCFNetworkProxiesHTTPSEnable] = 1 as NSNumber
                    collection[kCFNetworkProxiesHTTPSProxy] = proxyUrlSegments.2 as AnyObject?
                    collection[kCFNetworkProxiesHTTPSPort] = NSNumber(value: UInt16(proxyUrlSegments.3)!)
                    return true
                    
                default:
                    return false
            }
        }
        
        return false
    }

    /// Applies proxy settings to all applicable interfaces provided by the system
    /// - Parameters:
    ///   - preferences: The pre-authorized SCPreferences
    ///   - configuration: The configuration to apply
    internal static func applyProxySettings(preferences: SCPreferences, configuration: CFDictionary) {
        let proxySets = SCPreferencesGetValue(preferences, kSCPrefNetworkServices)!
        
        for proxyKey in proxySets.allKeys {
            let dict = proxySets.object(forKey: proxyKey)!
            let hwInterface = (dict as AnyObject).value(forKeyPath: "Interface.Hardware")
        
            if hwInterface != nil && ["AirPort","Wi-Fi","Ethernet"].contains(hwInterface as! String) {
                let keyName = "/\(kSCPrefNetworkServices)/\(proxyKey)/\(kSCEntNetProxies)"
                
                SCPreferencesPathSetValue(preferences, keyName as CFString, configuration as CFDictionary)
            }
        }
    }
}
