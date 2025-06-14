// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Reactive.Linq;
using System.Windows.Input;
using AppServiceSharp;
using AppServiceSharp.Enums;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using LucideAvalonia.Enum;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;

namespace DragonFruit.OnionFruit.MacOS.ViewModels
{
    public record ServiceStatusViewModel(string StatusText, IImmutableSolidColorBrush IconColor, bool ShowInstallButton, string InstallButtonText = "Install Service");

    public class ServiceManagementTabViewModel : ReactiveObject
    {
        private readonly AppService _daemonService;
        private readonly ObservableAsPropertyHelper<ServiceStatusViewModel> _serviceStatusContent;

        private AppServiceStatus? _serviceStatus;

        public ServiceManagementTabViewModel(IServiceProvider services)
        {
            _daemonService = services.GetKeyedService<AppService>("DaemonAppService");

            this.WhenAnyValue(x => x.ServiceStatus)
                .Select(UpdateServiceStatus)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.ServiceStatusContent, out _serviceStatusContent);

            RegisterService = ReactiveCommand.Create(TryRegisterService, this.WhenAnyValue(x => x.ServiceStatus).Select(x => x is not AppServiceStatus.Enabled and not AppServiceStatus.Unknown and not null));
            UnregisterService = ReactiveCommand.Create(TryUnregisterService, this.WhenAnyValue(x => x.ServiceStatus).Select(x => x is AppServiceStatus.Enabled));
            OpenSystemSettings = ReactiveCommand.Create(AppService.OpenSystemSettingsLoginItems);

            ServiceStatus = _daemonService?.Status;
        }

        public IconSource ServiceInfoIcon => App.GetIcon(LucideIconNames.Cog);
        public IconSource ServiceStatusIcon => App.GetIcon(LucideIconNames.Bolt);
        public IconSource ServiceAdministrationIcon => App.GetIcon(LucideIconNames.Wrench);

        public AppServiceStatus? ServiceStatus
        {
            get => _serviceStatus;
            private set => this.RaiseAndSetIfChanged(ref _serviceStatus, value);
        }

        public ServiceStatusViewModel ServiceStatusContent => _serviceStatusContent.Value;

        public ICommand RegisterService { get; }
        public ICommand UnregisterService { get; }
        public ICommand OpenSystemSettings { get; }

        private void TryRegisterService()
        {
            var result = _daemonService.RegisterService();

            if (result == ServiceUpdateError.LaunchDeniedByUser)
            {
                OpenSystemSettings.Execute(null);
                ServiceStatus = AppServiceStatus.RequiresApproval;
            }
            else
            {
                ServiceStatus = _daemonService.Status;
            }
        }

        private void TryUnregisterService()
        {
            _daemonService.UnregisterService();
            ServiceStatus = _daemonService.Status;
        }

        private static ServiceStatusViewModel UpdateServiceStatus(AppServiceStatus? serviceStatus) => serviceStatus switch
        {
            AppServiceStatus.Enabled => new ServiceStatusViewModel("Enabled", Brushes.LimeGreen, false),
            AppServiceStatus.NotFound => new ServiceStatusViewModel("Service Not Found", Brushes.OrangeRed, false), // not found = SMAppService can't install it
            AppServiceStatus.NotRegistered => new ServiceStatusViewModel("Not Registered", Brushes.Red, true),
            AppServiceStatus.RequiresApproval => new ServiceStatusViewModel("Pending User Approval", Brushes.Orange, true, "Continue Installation"),

            _ => new ServiceStatusViewModel("Service Status Unknown", Brushes.Gray, false)
        };
    }
}