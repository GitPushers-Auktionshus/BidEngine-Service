using BidWorker;
using NLog;
using NLog.Web;
using System.Text;
using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.Commons;
using BidWorker.Model;

var logger = NLog.LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
logger.Debug("init main");

try
{
    // Changed to using envirorment variables for deployment purposes
    //// Retrieves Vault hostname from dockercompose file
    //string hostnameVault = Environment.GetEnvironmentVariable("HostnameVault") ?? "none";

    //// Sets up the Vault using the endpoint of the Vault
    //var EndPoint = $"http://{hostnameVault}:8200/";
    //var httpClientHandler = new HttpClientHandler();
    //httpClientHandler.ServerCertificateCustomValidationCallback =
    //(message, cert, chain, sslPolicyErrors) => { return true; };

    //// Initialize one of the several auth methods.
    //IAuthMethodInfo authMethod =
    //new TokenAuthMethodInfo("00000000-0000-0000-0000-000000000000");

    //// Initialize vault settings.
    //var vaultClientSettings = new VaultClientSettings(EndPoint, authMethod)
    //{
    //    Namespace = "",
    //    MyHttpClientProviderFunc = handler => new HttpClient(httpClientHandler)
    //    {
    //        BaseAddress = new Uri(EndPoint)
    //    }
    //};

    //// Initialize vault client
    //IVaultClient vaultClient = new VaultClient(vaultClientSettings);

    //// Uses vault client to read key-value secrets.
    //Secret<SecretData> environmentVariables = await vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(path: "environmentVariables", mountPoint: "secret");
    //Secret<SecretData> connectionString = await vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(path: "connectionStrings", mountPoint: "secret");

    //// Initialized string variables to store enviroment secrets
    //string? secret = environmentVariables.Data.Data["Secret"].ToString();
    //string? issuer = environmentVariables.Data.Data["Issuer"].ToString();
    //string? connectionURI = connectionString.Data.Data["ConnectionURI"].ToString();

    // Creates and EnviromentVariable object with a dictionary to contain the secrets
    EnvVariables vaultSecrets = new EnvVariables
    {
        dictionary = new Dictionary<string, string>
        {
            { "Secret", Environment.GetEnvironmentVariable("Secret") ?? "none" },
            { "Issuer", Environment.GetEnvironmentVariable("Issuer") ?? "none" },
            { "ConnectionURI", Environment.GetEnvironmentVariable("ConnectionURI") ?? "none" }
        }
    };

    IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
        // Adds the EnviromentVariable object to the project as a singleton.
        // It can now be accessed wihtin the entire projekt
        services.AddSingleton<EnvVariables>(vaultSecrets);
    })
     .ConfigureLogging(logging =>
     {
         logging.ClearProviders();
     }).UseNLog()
    .Build();

    logger.Info($"Variables loaded in program.cs: Secret: {vaultSecrets.dictionary["Secret"]}, Issuer: {vaultSecrets.dictionary["Issuer"]}, ConnectionURI : {vaultSecrets.dictionary["ConnectionURI"]}");

    host.Run();

}
catch (Exception ex)
{
    logger.Error(ex, "Stopped program because of exception");
}
finally
{
    NLog.LogManager.Shutdown();
}