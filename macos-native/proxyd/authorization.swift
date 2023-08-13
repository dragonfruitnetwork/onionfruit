//
//  authorization.swift
//  proxyd
//
//  Created by Albie Spriddell on 11/08/2023.
//

import Foundation

let kAuthRightName    = "network.dragonfruit.proxyd.networkconfig";
let kAuthRightDesc    = "access and modify network proxy settings";
let kAuthRightDefault = kAuthorizationRuleAuthenticateAsAdmin;

func setupAuthorizationRights(authRef: inout AuthorizationRef) {
    var rightsError = AuthorizationRightGet(kAuthRightName, nil);
    
    if rightsError == errAuthorizationDenied {
        rightsError = AuthorizationRightSet(authRef, kAuthRightName, kAuthRightDefault as CFString, kAuthRightDesc as CFString, nil, nil);
        assert(rightsError == errAuthorizationSuccess)
    } else {
        // the right already exists, meaning this is run #2 or a setup script has already registered the right with the system
    }
}

func validateAuthorizationData(authData: Data) -> Bool {
    // validate data size
    if authData.count != kAuthorizationExternalFormLength {
        return false
    }
    
    var authRef: AuthorizationRef?
    var authRefExtForm = authData.withUnsafeBytes { bytes in bytes.load(as: AuthorizationExternalForm.self) }
    
    let parseError = AuthorizationCreateFromExternalForm(&authRefExtForm, &authRef)
    
    defer {
        // cleanup authref
        AuthorizationFree(authRef!, [])
    }
    
    if (parseError != errAuthorizationSuccess) {
        return false
    }
    
    let pointer = UnsafeMutablePointer<AuthorizationItem>.allocate(capacity: 1)
    let authItem = kAuthRightName.withCString { authorizationString in
        AuthorizationItem(name: authorizationString, valueLength: 0, value: nil, flags: 0)
    }

    pointer.initialize(to: authItem)

    defer {
        pointer.deinitialize(count: 1)
        pointer.deallocate()
    }
    
    var rights = AuthorizationRights(count: 1, items: pointer)
    let flags = AuthorizationFlags([.interactionAllowed, .extendRights, .preAuthorize])
    return AuthorizationCopyRights(authRef!, &rights, nil, flags, nil) == errAuthorizationSuccess
}
