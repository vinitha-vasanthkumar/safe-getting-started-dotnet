using Android.OS;
using Acr.UserDialogs;
using SafeTodoExample.Service;
using Android.App;
using Android.Content.PM;
using Android.Content;
using Xamarin.Forms;
using System;

namespace SafeTodoExample.Droid
{
    [Activity(
        Label = "SafeTodoExample",
        Icon = "@mipmap/icon",
        Theme = "@style/MainTheme",
        MainLauncher = true,
        LaunchMode = LaunchMode.SingleTask,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation),
        IntentFilter(
            new[] { Intent.ActionView },
            Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
            DataScheme = AppService.AppId
        )]
    public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity
    {
        private AppService AppService => DependencyService.Get<AppService>();

        protected override void OnCreate(Bundle bundle)
        {
            TabLayoutResource = Resource.Layout.Tabbar;
            ToolbarResource = Resource.Layout.Toolbar;

            base.OnCreate(bundle);

            Rg.Plugins.Popup.Popup.Init(this, bundle);
            UserDialogs.Init(this);

            Forms.Init(this, bundle);
            LoadApplication(new App());

            if (Intent?.Data != null)
            {
                HandleAppLaunch(Intent.Data.ToString());
            }
        }

        protected override void OnNewIntent(Intent intent)
        {
            base.OnNewIntent(intent);
            if (intent?.Data != null)
            {
                HandleAppLaunch(intent.Data.ToString());
            }
        }

        private void HandleAppLaunch(string url)
        {
            System.Diagnostics.Debug.WriteLine($"Launched via: {url}");
            Device.BeginInvokeOnMainThread(
              async () =>
              {
                  try
                  {
                      await AppService.HandleUrlActivationAsync(url);
                      System.Diagnostics.Debug.WriteLine("IPC Msg Handling Completed");
                  }
                  catch (Exception ex)
                  {
                      System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                  }
              });
        }
    }
}

