//
//  ContentView.swift
//  daemonloader
//
//  Created by Albie Spriddell on 04/06/2025.
//

import ServiceManagement
import SwiftUI

@MainActor
class SMAppServiceViewModel: ObservableObject {
    @Published var status: String = "Unknown"
    @Published var statusColor: Color = .gray

    var appService: SMAppService = SMAppService.daemon(plistName: "network.dragonfruit.onionfruitd-dev.plist")

    init() {
        getStatus()
    }

    func getStatus() {
        Task {
            switch appService.status {
            case .notRegistered:
                status = "Not Installed"
                statusColor = .red
            case .enabled:
                status = "Installed"
                statusColor = .green
            case .notFound:
                status = "Service not found"
                statusColor = .gray
            case .requiresApproval:
                status = "Pending user approval in settings"
                statusColor = .orange
            @unknown default:
                status = "Unknown status"
                statusColor = .gray
            }
        }
    }

    func install() {
        Task {
            do {
                try appService.register()
                getStatus()
            }
            catch {
                let nsError = error as NSError
                
                switch (nsError.code) {
                case kSMErrorAlreadyRegistered:
                    getStatus()
                case kSMErrorInvalidSignature:
                    status = "Installation failed: Invalid signature"
                    statusColor = .gray
                default:
                    status = "Installation failed: \(nsError.localizedDescription)"
                    statusColor = .red
                }
            }
        }
    }

    func uninstall() {
        Task {
            do {
                try await appService.unregister()
                getStatus()
            } catch {
                status = "Uninstall failed: \(error.localizedDescription)"
            }
        }
    }

    func openSettings() {
        SMAppService.openSystemSettingsLoginItems()
    }
}

struct ContentView: View {
    @StateObject private var viewModel = SMAppServiceViewModel()

    var body: some View {
        VStack(spacing: 20) {
            HStack {
                Circle()
                    .fill(viewModel.statusColor)
                    .frame(width: 10, height: 10)
                Text(viewModel.status)
                    .padding(.leading, 5)
            }
            HStack {
                Button("Install") {
                    viewModel.install()
                }
                Button("Uninstall") {
                    viewModel.uninstall()
                }
                
                Divider()
                    .frame(height: 15)
                    .padding(.horizontal, 5)
                
                Button("Open Settings") {
                    viewModel.openSettings()
                }
            }
        }
        .padding()
        .frame(width: 500, height: 150)
    }
}

