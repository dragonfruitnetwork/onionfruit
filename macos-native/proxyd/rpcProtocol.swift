//
//  rpcProtocol.swift
//  proxyd
//
//  Created by Albie Spriddell on 11/08/2023.
//

import Foundation

@objc(RpcProtocol) protocol RpcProtocol {
    func setProxy(_ url: String, withReply reply: @escaping (Bool) -> Void)
    func clearProxies(withReply reply: @escaping (Bool) -> Void)
    func getVersion(withReply reply: @escaping (UInt16) -> Void)
    func getUid(withReply reply: @escaping (String) -> Void)
}
