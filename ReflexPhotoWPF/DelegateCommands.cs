/*******************************************************/
/* Copyright ReflexPhoto © 2020 - Tous droits réservés */
/* https://reflexphoto.eu <dev@reflexphoto.eu>         */
/*******************************************************/
using System;
using System.Windows.Input;

namespace ReflexPhotoWPF
{
    public class ShowWindowCommand : ICommand
    {
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter)
        {
            if (App.Current.MainWindow != null)
            {
                App.Current.MainWindow.Close();
                App.Current.MainWindow = null;
            }
            if (App.Current.MainWindow == null)
            {
                App.Current.MainWindow = new MainWindow();
                App.Current.MainWindow.Show();
            }
        }
    }

    public class HideWindowCommand : ICommand
    {
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter)
        {
            if (App.Current.MainWindow != null)
                App.Current.MainWindow.Close();
            App.Current.MainWindow = null;
        }
    }

    public class ShowWebsiteCommand : ICommand
    {
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) => true;

#pragma warning disable CS4014 // No need to wait.
        public void Execute(object parameter) => Windows.System.Launcher.LaunchUriAsync(new Uri("https://reflexphoto.eu"));
#pragma warning restore CS4014
    }

    public class ExitApplicationCommand : ICommand
    {
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter) => App.StopApp(0);
    }
}
