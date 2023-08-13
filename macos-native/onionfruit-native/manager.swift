//
//  manager.swift
//  onionfruit-native
//
//  Created by Albie Spriddell on 13/08/2023.
//

import Foundation
import ServiceManagement

class DaemonManager {
    
    private let agent: SMAppService;

    init() {
        agent = SMAppService.daemon(plistName: "network.dragonfruit.proxyd.plist")
    }

    var installState: SMAppService.Status {
        get {
            return agent.status
        }
    }
    
    func installDaemon() -> Int {
        do {
            try agent.register()
            return 0
        } catch {
            return (error as NSError).code
        }
    }
    
    func removeDaemon() -> Int {
        do {
            try agent.unregister()
            return 0
        } catch {
            return (error as NSError).code
        }
    }
}
