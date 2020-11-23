/*******************************************************/
/* Copyright ReflexPhoto © 2020 - Tous droits réservés */
/* https://reflexphoto.eu <dev@reflexphoto.eu>         */
/*******************************************************/
using System.Windows.Input;

namespace ReflexPhotoWPF
{
    public class NotifyIconViewModel
    {
        /// <summary>Shows a window, if none is already open.</summary>
        public ICommand ShowWindowCommand { get => new ShowWindowCommand(); }

        /// <summary>Hides the main window. This command is only enabled if a window is open.</summary>
        public ICommand HideWindowCommand { get => new HideWindowCommand(); }

        /// <summary>Opens ReflexPhoto website.</summary>
        public ICommand ShowWebsiteCommand { get => new ShowWebsiteCommand(); }

        /// <summary>Shuts down the application.</summary>
        public ICommand ExitApplicationCommand { get => new ExitApplicationCommand(); }
    }
}
