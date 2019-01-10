using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SafeApp;
using SafeApp.Utilities;
using SafeTodoExample.Helpers;
using SafeTodoExample.Model;
using SafeTodoExample.Service;
using Xamarin.Forms;
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
        private string mdByteList = "MySafeTodo";
        public const string AuthDeniedMessage = "Failed to receive Authentication.";
        private static bool _newMdInfoFlag;
        private Session _session;
        private bool _mDataAvailable;
        private MDataInfo _mDataInfo;

        public bool IsSessionAvailable => _session != null ? true : false;

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
            _mDataAvailable = false;
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
                var appContainerMDataInfo = await _session.AccessContainer.GetMDataInfoAsync("apps/" + AppId);
                var encrypedAppKey = await _session.MDataInfoActions.EncryptEntryKeyAsync(appContainerMDataInfo, mdByteList.ToUtfBytes());
                (List<byte>, ulong) encryptedValue = await _session.MData.GetValueAsync(appContainerMDataInfo, encrypedAppKey);
                if (encryptedValue.Item1 != null)
                {
                    var plainValue = await _session.MDataInfoActions.DecryptAsync(appContainerMDataInfo, encryptedValue.Item1);
                    _mDataInfo = await _session.MDataInfoActions.DeserialiseAsync(plainValue);
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
                throw ex;
            }
            _mDataAvailable = true;
        }

        public async Task StoreMdInfoAsync()
        {
            try
            {
                if (_newMdInfoFlag)
                {
                    var serializedMDataInfo = await _session.MDataInfoActions.SerialiseAsync(_mDataInfo);
                    var appContainerMDataInfo = await _session.AccessContainer.GetMDataInfoAsync("apps/" + AppId);
                    var encrypedAppKey = await _session.MDataInfoActions.EncryptEntryKeyAsync(appContainerMDataInfo, mdByteList.ToUtfBytes());
                    var encryptedMDataInfo = await _session.MDataInfoActions.EncryptEntryValueAsync(appContainerMDataInfo, serializedMDataInfo);
                    using (var appContEntActH = await _session.MDataEntryActions.NewAsync())
                    {
                        await _session.MDataEntryActions.InsertAsync(appContEntActH, encrypedAppKey, encryptedMDataInfo);
                        await _session.MData.MutateEntriesAsync(appContainerMDataInfo, appContEntActH);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error : " + ex.Message);
                throw ex;
            }
            _newMdInfoFlag = false;
        }

        public async Task<List<TodoItem>> GetItemAsync()
        {
            List<TodoItem> messages = new List<TodoItem>();
            try
            {
                if (!_mDataAvailable)
                {
                    await GetMdInfoAsync();
                }

                if (!_newMdInfoFlag)
                {
                    using (var entriesHandle = await _session.MDataEntries.GetHandleAsync(_mDataInfo))
                    {
                        var encryptedEntries = await _session.MData.ListEntriesAsync(entriesHandle);
                        foreach (var entry in encryptedEntries)
                        {
                            if (entry.Value.Content.Count > 0)
                            {
                                var decryptedKey = await _session.MDataInfoActions.DecryptAsync(_mDataInfo, entry.Key.Key.ToList());
                                var decryptedValue = await _session.MDataInfoActions.DecryptAsync(_mDataInfo, entry.Value.Content.ToList());
                                var deserializedValue = decryptedValue.Deserialize();
                                messages.Add(deserializedValue as TodoItem);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error : " + ex.Message);
                throw ex;
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
                        var mDataPermissionSet = new PermissionSet { Insert = true, ManagePermissions = true, Read = true, Update = true, Delete = true };
                        using (NativeHandle permissionsH = await _session.MDataPermissions.NewAsync())
                        using (NativeHandle appSignKeyH = await _session.Crypto.AppPubSignKeyAsync())
                        {
                            await _session.MDataPermissions.InsertAsync(permissionsH, appSignKeyH, mDataPermissionSet);
                            var encryptedKey = await _session.MDataInfoActions.EncryptEntryKeyAsync(_mDataInfo, todoItem.Title.ToUtfBytes());
                            var encryptedValue = await _session.MDataInfoActions.EncryptEntryValueAsync(_mDataInfo, todoItem.Serialize());
                            await _session.MDataEntries.InsertAsync(entriesHandle, encryptedKey, encryptedValue);
                            await _session.MData.PutAsync(_mDataInfo, permissionsH, entriesHandle);
                        }
                        await StoreMdInfoAsync();
                    }
                }
                else
                {
                    using (NativeHandle entriesHandle = await _session.MDataEntryActions.NewAsync())
                    {
                        var encryptedKey = await _session.MDataInfoActions.EncryptEntryKeyAsync(_mDataInfo, todoItem.Title.ToUtfBytes());
                        var encryptedValue = await _session.MDataInfoActions.EncryptEntryValueAsync(_mDataInfo, todoItem.Serialize());
                        await _session.MDataEntryActions.InsertAsync(entriesHandle, encryptedKey, encryptedValue);
                        await _session.MData.MutateEntriesAsync(_mDataInfo, entriesHandle);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error : " + ex.Message);
                throw ex;
            }
        }

        public async Task UpdateItemAsync(TodoItem todoItem)
        {
            try
            {
                using (var entriesHandle = await _session.MDataEntryActions.NewAsync())
                {
                    var keyToUpdate = await _session.MDataInfoActions.EncryptEntryKeyAsync(_mDataInfo, todoItem.Title.ToUtfBytes());
                    var newValueToUpdate = await _session.MDataInfoActions.EncryptEntryValueAsync(_mDataInfo, todoItem.Serialize());
                    var value = await _session.MData.GetValueAsync(_mDataInfo, keyToUpdate);
                    await _session.MDataEntryActions.UpdateAsync(entriesHandle, keyToUpdate, newValueToUpdate, value.Item2 + 1);
                    await _session.MData.MutateEntriesAsync(_mDataInfo, entriesHandle);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error : " + ex.Message);
                throw ex;
            }
        }

        public async Task DeleteItemAsync(TodoItem todoItem)
        {
            try
            {
                using (var entriesHandle = await _session.MDataEntryActions.NewAsync())
                {
                    var keyToDelete = await _session.MDataInfoActions.EncryptEntryKeyAsync(_mDataInfo, todoItem.Title.ToUtfBytes());
                    var value = await _session.MData.GetValueAsync(_mDataInfo, keyToDelete);
                    await _session.MDataEntryActions.DeleteAsync(entriesHandle, keyToDelete, value.Item2 + 1);
                    await _session.MData.MutateEntriesAsync(_mDataInfo, entriesHandle);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error : " + ex.Message);
                throw ex;
            }
        }

        #endregion

        #region Test Network Authentication

        public async Task<string> GenerateAppRequestAsync()
        {
            AuthReq authReq = new AuthReq
            {
                AppContainer = true,
                App = new AppExchangeInfo { Id = AppId, Scope = string.Empty, Name = "SAFE Todo App", Vendor = "MaidSafe.net Ltd" },
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
                        DialogHelper.ShowToast("Auth Granted", DialogType.Success);
                        MessagingCenter.Send(this, MessengerConstants.NavigateToItemPage);
                    }
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
            Debug.WriteLine("Log-in Successfully");
        }

        private async Task CreateDemoTodoApp()
        {
            AuthReq authReq = new AuthReq
            {
                AppContainer = true,
                App = new AppExchangeInfo { Id = AppId, Scope = string.Empty, Name = AppName, Vendor = "MaidSafe.net Ltd" },
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
            await Task.Run(() =>
            {
                _authenticator.Dispose();
                Dispose();
            });
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
