using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Foundation;
using SafeTodoExample.Service;
using UIKit;
using Xamarin.Forms;

namespace SafeTodoExample.iOS
{
    // The UIApplicationDelegate for the application. This class is responsible for launching the 
    // User Interface of the application, as well as listening (and optionally responding) to 
    // application events from iOS.
    [Register("AppDelegate")]
    public partial class AppDelegate : global::Xamarin.Forms.Platform.iOS.FormsApplicationDelegate
    {
        public AppService AppService => DependencyService.Get<AppService>();
        //
        // This method is invoked when the application has loaded and is ready to run. In this 
        // method you should instantiate the window, load the UI into it and then make the window
        // visible.
        //
        // You have 17 seconds to return from this method, or iOS will terminate your application.
        //
        public override bool FinishedLaunching(UIApplication app, NSDictionary options)
        {

            Rg.Plugins.Popup.Popup.Init();
            global::Xamarin.Forms.Forms.Init();
            LoadApplication(new App());

            return base.FinishedLaunching(app, options);
        }

        public override bool OpenUrl(UIApplication app, NSUrl url, NSDictionary options)
        {
            Device.BeginInvokeOnMainThread(
              async () => {
                  try
                  {
                      await AppService.HandleUrlActivationAsync(url.ToString());
                      Debug.WriteLine("IPC Msg Handling Completed");
                  }
                  catch (Exception ex)
                  {
                      Debug.WriteLine($"Error: {ex.Message}");
                  }
              });
            return true;
        }
    }
}
