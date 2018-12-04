using System;
using System.Threading;
using System.Threading.Tasks;
using App;
using App.Network;
using SharedDemoCode;
using SharedDemoCode.Network;

namespace MockNetworkExample
{
    internal class Program
    {
        // private static readonly ManualResetEvent resetEvent = new ManualResetEvent(false);
        private static Mutex _mutex;
        private static bool _firstApplicationInstance;

        private static async Task Main()
        {
            string[] args = Environment.GetCommandLineArgs();
            Console.WriteLine("SafeNetwork Console Application");
            try
            {
                if (IsApplicationFirstInstance())
                {
                    Console.Write("Press Y to use mock safe-browser for authentication : ");
                    var key = Console.ReadKey().KeyChar;
                    Console.WriteLine();

                    if (key == 'Y' || key == 'y')
                    {
                        // args[0] is always the path to the application
                        // update system registery
                        App.Helpers.RegisterAppProtocol(args[0]);

                        // Request authentication from mock browser
                        await Authentication.MockAuthenticationWithBrowserAsync();

                        // Start named pipe server and listen for message
                        var authResponse = PipeComm.ReceiveNamedPipeServerMessage();

                        if (!string.IsNullOrEmpty(authResponse))
                        {
                            // Create session from response
                            await Authentication.ProcessAuthenticationResponse(authResponse);

                            // Show user menu
                            UserInput userInput = new UserInput();
                            await userInput.ShowUserOptions();
                        }
                    }
                    else
                    {
                        // Create session from mock authentication
                        var session = await Authentication.MockAuthenticationAsync();

                        // Initialise session for Mutable Data operations
                        MutableDataOperations.InitialiseSession(session);

                        // Show user menu
                        UserInput userInput = new UserInput();
                        await userInput.ShowUserOptions();
                    }
                }
                else
                {
                    // We are not the first instance, send the named pipe message with our payload and stop loading
                    if (args.Length >= 2)
                    {
                        var namedPipePayload = new NamedPipePayload
                        {
                            SignalQuit = false,
                            Arguments = args[1]
                        };

                        // Send the message
                        PipeComm.SendNamedPipeClient(namedPipePayload);
                    }

                    // Close app
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
            }
            Console.ReadLine();
        }

        private static bool IsApplicationFirstInstance()
        {
            // Allow for multiple runs but only try and get the mutex once
            if (_mutex == null)
            {
                _mutex = new Mutex(true, ConsoleAppConstants.AppName, out _firstApplicationInstance);
            }
            return _firstApplicationInstance;
        }
    }
}
