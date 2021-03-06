﻿using System;
using Acr.UserDialogs;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using SafeTodoExample.Service;
using Xamarin.Forms;

namespace SafeTodoExample.Droid
{
    [Activity(
        Label = "SafeTodoExample",
        Icon = "@mipmap/icon",
        Theme = "@style/MainTheme",
        MainLauncher = true,
        LaunchMode = LaunchMode.SingleTask,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
    [IntentFilter(
            new[] { Intent.ActionView },
            Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
            DataScheme = AppService.AppId)]
    public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity
    {
        private AppService AppService => DependencyService.Get<AppService>();

        long _lastPress;

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

        public override void OnBackPressed()
        {
            long currentTime = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

            if (currentTime - _lastPress > 5000)
            {
                Toast.MakeText(this, "Press back again to exit", ToastLength.Short).Show();
                _lastPress = currentTime;
            }
            else
            {
                base.OnBackPressed();
                Java.Lang.JavaSystem.Exit(0);
            }
        }
    }
}
