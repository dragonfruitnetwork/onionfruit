// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml.Templates;

namespace DragonFruit.OnionFruit.Controls
{
    public class SwitchingControl : UserControl
    {
        private readonly ContentPresenter _presenter;

        public static readonly StyledProperty<bool> SwitchProperty = AvaloniaProperty.Register<SwitchingControl, bool>(nameof(Switch));
        public static readonly StyledProperty<IDataTemplate> SwitchTrueProperty = AvaloniaProperty.Register<SwitchingControl, IDataTemplate>(nameof(SwitchTrue), defaultValue: new DataTemplate());
        public static readonly StyledProperty<IDataTemplate> SwitchFalseProperty = AvaloniaProperty.Register<SwitchingControl, IDataTemplate>(nameof(SwitchFalse), defaultValue: new DataTemplate());

        public SwitchingControl()
        {
            Content = _presenter = new ContentPresenter();
        }

        protected override void OnInitialized()
        {
            var o = this.GetObservable(SwitchProperty).CombineLatest(this.GetObservable(SwitchTrueProperty), this.GetObservable(SwitchFalseProperty));

            _presenter.Bind(ContentPresenter.ContentProperty, this.GetObservable(DataContextProperty));
            _presenter.Bind(ContentPresenter.ContentTemplateProperty, o.Select(tuple => tuple.Item1 ? tuple.Item2 : tuple.Item3));
        }

        public bool Switch
        {
            get => GetValue(SwitchProperty);
            set => SetValue(SwitchProperty, value);
        }

        public IDataTemplate SwitchTrue
        {
            get => GetValue(SwitchTrueProperty);
            set => SetValue(SwitchTrueProperty, value);
        }

        public IDataTemplate SwitchFalse
        {
            get => GetValue(SwitchFalseProperty);
            set => SetValue(SwitchFalseProperty, value);
        }
    }
}