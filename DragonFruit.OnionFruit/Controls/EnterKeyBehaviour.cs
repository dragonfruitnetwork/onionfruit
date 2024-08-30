// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using Avalonia;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using System.Windows.Input;

namespace DragonFruit.OnionFruit.Controls
{
    public class EnterKeyBehavior : Behavior<InputElement>
    {
        public static readonly StyledProperty<ICommand> CommandProperty = AvaloniaProperty.Register<EnterKeyBehavior, ICommand>(nameof(Command));

        public ICommand Command
        {
            get => GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.KeyDown += OnKeyDown;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.KeyDown -= OnKeyDown;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Command?.CanExecute(null) == true)
            {
                Command.Execute(null);
            }
        }
    }
}