//
//  interop.swift
//  onionfruit-native
//
//  Created by Albie Spriddell on 13/08/2023.
//

import Foundation
import ServiceManagement

@_cdecl("getApiVersion")
func getApiVersion() -> UInt16 {
    return 1;
}

@_cdecl("createManager")
/// Creates a new Daemon Manager
/// - Returns: A pointer to the created resource
public func createDaemonManager() -> OpaquePointer {
    let type = DaemonManager()
    let retained = Unmanaged.passRetained(type).toOpaque()

    return OpaquePointer(retained)
}

@_cdecl("closeManager")
/// Disposes the manager at the provided address
/// - Parameter handle: The handle of the manager to dispose
public func disposeDaemonManager(_ handle: OpaquePointer) -> Void {
    _ = getDaemonFromPointer(handle).takeRetainedValue()
}

@_cdecl("getInstallState")
/// Gets the current state of the daemon
/// - Parameter handle: Handle of the current manager
/// - Returns: The current installation state of the daemon
public func getDaemonManagerType(_ handle: OpaquePointer) -> CInt {
    let daemonManager = getDaemonFromPointer(handle).takeUnretainedValue()
    return CInt(daemonManager.installState.rawValue)
}

@_cdecl("performRegistration")
/// Performs installation/registration of the daemon
/// - Parameters:
///   - handle: Handle of the current manager
///   - newState: (Output) The new state of the daemon after registration process has finished
/// - Returns: If an error occured during registration, the error code that was returned
public func performDaemonInstallation(_ handle: OpaquePointer, newState: UnsafeMutablePointer<CInt>) -> CInt {
    let daemonManager = getDaemonFromPointer(handle).takeUnretainedValue()
    let installSuccess = daemonManager.installDaemon()
    
    newState.pointee = CInt(daemonManager.installState.rawValue)
    
    return CInt(installSuccess)
}

@_cdecl("openLoginItemsSettings")
/// Opens the system settings login items page, if available
/// - Returns: Whether the window was opened
public func openLoginItemsSettings() -> Bool {
    if ProcessInfo().isOperatingSystemAtLeast(OperatingSystemVersion(majorVersion: 13, minorVersion: 0, patchVersion: 0)) {
        SMAppService.openSystemSettingsLoginItems()
        return true
    } else {
        return false
    }
}

@_cdecl("performRemoval")
/// Removes/Unregisters the daemon from the service controller.
/// - Parameters:
///   - handle: Handle of the current manager
///   - newState: (Output) The new state of the daemon after completion
/// - Returns: If an error occured during the unregistration method, the error code that was returned
public func performDaemonRemoval(_ handle: OpaquePointer, newState: UnsafeMutablePointer<CInt>) -> CInt {
    let daemonManager = getDaemonFromPointer(handle).takeUnretainedValue()
    let installSuccess = daemonManager.removeDaemon()
    
    newState.pointee = CInt(daemonManager.installState.rawValue)
    
    return CInt(installSuccess)
}

private func getDaemonFromPointer(_ ptr: OpaquePointer) -> Unmanaged<DaemonManager> {
    return Unmanaged<DaemonManager>.fromOpaque(UnsafeRawPointer(ptr))
}
