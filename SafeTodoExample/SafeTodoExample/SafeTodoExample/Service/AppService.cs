using SafeApp;
using SafeApp.Utilities;
using SafeTodoExample.Model;
using SafeTodoExample.Service;
using System;
using System.Collections.Generic;
using Xamarin.Forms;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.ObjectModel;
using SafeTodoExample.Helpers;
#if SAFE_APP_MOCK
using SafeApp.MockAuthBindings;
#endif

[assembly: Dependency(typeof(AppService))]

namespace SafeTodoExample.Service
{
    public class AppService : ObservableObject, IDisposable
    {
        public const string AppId = "net.maidsafe.examples.todo";
        public const string AppName = "Safe Todo";
        private string mdByteList = "MySafeToDo";
        public const string AuthDeniedMessage = "Failed to receive Authentication.";
        private Session _session;
        public static bool _newMdInfoFlag = false;
        public bool _mDataAvilable = false;
        private MDataInfo _mDataInfo;

        public AppService()
        {
            Session.Disconnected += OnSessionDisconnected;
        }

        public void Dispose()
        {
            FreeState();
            GC.SuppressFinalize(this);
        }

        ~AppService()
        {
            FreeState();
        }

        public void FreeState()
        {
            Session.Disconnected -= OnSessionDisconnected;
            _session?.Dispose();
            _session = null;
            _mDataInfo.Name = null;
            _newMdInfoFlag = false;
            _mDataAvilable = false;
        }

        private void OnSessionDisconnected(object obj, EventArgs e)
        {
            if (!obj.Equals(_session))
            {
                return;
            }

            Device.BeginInvokeOnMainThread(
              () =>
              {
                  _session?.Dispose();
              });
        }

        #region MutableData Operation

        private async Task GetMdInfoAsync()
        {
            try
            {
                MDataInfo appCont = await _session.AccessContainer.GetMDataInfoAsync("apps/" + AppId);
                List<byte> cipherBytes = await _session.MDataInfoActions.EncryptEntryKeyAsync(appCont, mdByteList.ToUtfBytes());
                (List<byte>, ulong) content = await _session.MData.GetValueAsync(appCont, cipherBytes);
                if (content.Item1 != null)
                {
                    List<byte> plainBytes = await _session.MDataInfoActions.DecryptAsync(appCont, content.Item1);
                    _mDataInfo = await _session.MDataInfoActions.DeserialiseAsync(plainBytes);
                }
            }
            catch (FfiException ex)
            {
                Debug.WriteLine("Error : " + ex.Message);
                const ulong tagType = 16000;
                _mDataInfo = await _session.MDataInfoActions.RandomPrivateAsync(tagType);
                _newMdInfoFlag = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error : " + ex.Message);
            }
            _mDataAvilable = true;
        }

        public async Task StoreMdInfoAsync()
        {
            try
            {
                if (_newMdInfoFlag)
                {
                    List<byte> serializedData = await _session.MDataInfoActions.SerialiseAsync(_mDataInfo);
                    MDataInfo appContH = await _session.AccessContainer.GetMDataInfoAsync("apps/" + AppId);
                    List<byte> userIdCipherBytes = await _session.MDataInfoActions.EncryptEntryKeyAsync(appContH, mdByteList.ToUtfBytes());
                    List<byte> serializedCipherBytes = await _session.MDataInfoActions.EncryptEntryValueAsync(appContH, serializedData);
                    using (NativeHandle appContEntActH = await _session.MDataEntryActions.NewAsync())
                    {
                        await _session.MDataEntryActions.InsertAsync(appContEntActH, userIdCipherBytes, serializedCipherBytes);
                        await _session.MData.MutateEntriesAsync(appContH, appContEntActH);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error : " + ex.Message);
            }
            _newMdInfoFlag = false;
        }

        public async Task<ObservableCollection<TodoItem>> GetItemAsync()
        {
            ObservableCollection<TodoItem> messages = new ObservableCollection<TodoItem>();
            try
            {
                if (!_mDataAvilable)
                {
                    await GetMdInfoAsync();
                }

                if (!_newMdInfoFlag)
                {
                    NativeHandle entiredH = await _session.MDataEntries.GetHandleAsync(_mDataInfo);
                    List<MDataEntry> entries = await _session.MData.ListEntriesAsync(entiredH);
                    foreach (MDataEntry item in entries)
                    {
                        var plainkey = item.Key.Val;
                        var vl = await _session.MData.GetValueAsync(_mDataInfo, item.Key.Val);
                        var plainvalue = vl.Item1;
                        Debug.WriteLine("Key : {0}, Value : {1}", plainkey.ToUtfString(), plainvalue.ToUtfString());
                        if (plainvalue.Count > 0)
                        {
                            messages.Add(new TodoItem() { Title = plainkey.ToUtfString(), Detail = plainvalue.ToUtfString() });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error : " + ex.Message);
            }
            return messages;
        }

        public async Task AddItemAsync(TodoItem todoItem)
        {
            try
            {
                if (_newMdInfoFlag)
                {
                    using (NativeHandle entriesHandle = await _session.MDataEntries.NewAsync())
                    {
                        using (NativeHandle permissions = await _session.MDataPermissions.NewAsync())
                        using (NativeHandle appKey = await _session.Crypto.AppPubSignKeyAsync())
                        {
                            await _session.MDataPermissions.InsertAsync(
                              permissions, appKey, new PermissionSet { Insert = true, Delete = true, Update = true, Read = true });
                            await _session.MDataEntries.InsertAsync(entriesHandle, todoItem.Title.ToUtfBytes(), todoItem.Detail.ToUtfBytes());
                            await _session.MData.PutAsync(_mDataInfo, permissions, entriesHandle);
                        }
                        await StoreMdInfoAsync();
                    }
                }
                else
                {
                    using (NativeHandle entriesHandle = await _session.MDataEntryActions.NewAsync())
                    {
                        await _session.MDataEntryActions.InsertAsync(entriesHandle, todoItem.Title.ToUtfBytes(), todoItem.Detail.ToUtfBytes());
                        await _session.MData.MutateEntriesAsync(_mDataInfo, entriesHandle);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error : " + ex.Message);
            }
        }

        public async Task UpdateItemAsync(TodoItem todoItem)
        {
            try
            {
                using (var entriesHandle = await _session.MDataEntryActions.NewAsync())
                {
                    var keyToUpdate = todoItem.Title.ToUtfBytes();
                    var value = await _session.MData.GetValueAsync(_mDataInfo, keyToUpdate);
                    await _session.MDataEntryActions.UpdateAsync(entriesHandle, keyToUpdate, todoItem.Detail.ToUtfBytes(), value.Item2 + 1);
                    await _session.MData.MutateEntriesAsync(_mDataInfo, entriesHandle);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error : " + ex.Message);
            }
        }

        public async Task DeleteItemAsync(TodoItem todoItem)
        {
            try
            {
                using (var entriesHandle = await _session.MDataEntryActions.NewAsync())
                {
                    var keyToDelete = todoItem.Title.ToUtfBytes();
                    var value = await _session.MData.GetValueAsync(_mDataInfo, keyToDelete);
                    await _session.MDataEntryActions.DeleteAsync(entriesHandle, keyToDelete, value.Item2 + 1);
                    await _session.MData.MutateEntriesAsync(_mDataInfo, entriesHandle);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error : " + ex.Message);
            }
        }


        #endregion

        #region Test Network Authentication

        public async Task<string> GenerateAppRequestAsync()
        {
            AuthReq authReq = new AuthReq
            {
                AppContainer = true,
                App = new AppExchangeInfo { Id = AppId, Scope = "", Name = "SAFE Todo App", Vendor = "MaidSafe.net Ltd" },
                Containers = new List<ContainerPermissions>
                {
                    new ContainerPermissions
                    {
                        ContName = "_publicNames",
                        Access = { Insert = true, Update = true, Delete = true }
                    }
                }
            };

            (uint, string) encodedReq = await Session.EncodeAuthReqAsync(authReq);
            string formattedReq = UrlFormat.Format(AppId, encodedReq.Item2, true);
            Debug.WriteLine($"Encoded Req: {formattedReq}");
            return formattedReq;
        }

        public async Task HandleUrlActivationAsync(string url)
        {
            try
            {
                string encodedRequest = UrlFormat.GetRequestData(url);
                IpcMsg decodeResult = await Session.DecodeIpcMessageAsync(encodedRequest);
                if (decodeResult.GetType() == typeof(AuthIpcMsg))
                {
                    Debug.WriteLine("Received Auth Granted from Authenticator");
                    AuthIpcMsg ipcMsg = decodeResult as AuthIpcMsg;
                    if (ipcMsg != null)
                    {
                        _session = await Session.AppRegisteredAsync(AppId, ipcMsg.AuthGranted);
                    }

                    DialogHelper.ShowToast("Auth Granted", DialogType.Success);
                    MessagingCenter.Send(this, MessengerConstants.NavigateToItemPage);
                }
                else
                {
                    Debug.WriteLine("Decoded Req is not Auth Granted");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Description: {ex.Message}", "OK");
                DialogHelper.ShowToast(AuthDeniedMessage, DialogType.Error);
            }
        }

        #endregion

        #region Mock Authentication
#if SAFE_APP_MOCK
        private Authenticator _authenticator;

        private async Task CreateAndLoginAccountAsync()
        {
            string location = Misc.GetRandomString(10);
            string password = Misc.GetRandomString(10);
            string invitation = Misc.GetRandomString(15);
            Debug.WriteLine($"CreateAccountAsync: {location} - {password} - {invitation.Substring(0, 5)}");
            _authenticator = await Authenticator.CreateAccountAsync(location, password, invitation);
            Debug.WriteLine("Account Created Successfully");
            await LoginAsync(location, password);
        }

        private async Task LoginAsync(string location, string password)
        {
            Debug.WriteLine($"LoginAsync: {location} - {password}");
            _authenticator = await Authenticator.LoginAsync(location, password);
            Debug.WriteLine("Loggedin Successfully");
        }

        private async Task CreateDemoTodoApp()
        {
            AuthReq authReq = new AuthReq
            {
                AppContainer = true,
                App = new AppExchangeInfo { Id = AppId, Scope = "", Name = AppName, Vendor = "MaidSafe.net Ltd" },
                Containers = new List<ContainerPermissions> { new ContainerPermissions { ContName = "_publicNames", Access = { Insert = true, Update = true, Delete = true } } }
            };

            Debug.WriteLine($"Create Test App: {AppName} - {AppId}");
            _session = await CreateTestAppAsync(authReq);
            Debug.WriteLine($"App Created Successfully");
        }

        private async Task<Session> CreateTestAppAsync(AuthReq authReq)
        {
            (uint _, string reqMsg) = await Session.EncodeAuthReqAsync(authReq);
            IpcReq ipcReq = await _authenticator.DecodeIpcMessageAsync(reqMsg);
            AuthIpcReq authIpcReq = ipcReq as AuthIpcReq;
            string resMsg = await _authenticator.EncodeAuthRespAsync(authIpcReq, true);
            IpcMsg ipcResponse = await Session.DecodeIpcMessageAsync(resMsg);
            AuthIpcMsg authResponse = ipcResponse as AuthIpcMsg;
            return await Session.AppRegisteredAsync(authReq.App.Id, authResponse.AuthGranted);
        }

        public async Task ProcessMockAuthentication()
        {
            await CreateAndLoginAccountAsync();
            await CreateDemoTodoApp();
        }

        public async Task LogoutAsync()
        {
            await Task.Run(() => { _authenticator.Dispose(); Dispose(); });
        }
#else
        public async Task LogoutAsync()
        {
            await Task.Run(() => { Dispose(); });
        }
#endif
        #endregion
    }
}
