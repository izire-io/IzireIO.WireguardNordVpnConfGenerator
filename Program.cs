using IzireIO.NordVpn.Api.Client;
using IzireIO.NordVpn.Api.Client.Enum;

#region Parameter retrieval
var nordVpnWireguardPrivateKey = Environment.GetEnvironmentVariable("IIO_WNCG_WIREGUARD_PRIVATE_KEY") ?? "";
if (string.IsNullOrEmpty(nordVpnWireguardPrivateKey))
{
    Console.Error.WriteLine("Missing IIO_WNCG_WIREGUARD_PRIVATE_KEY environment variable.");
    Environment.ExitCode = 1;
    return;
}

var selectedLocations = new List<CountryId>();
foreach(var rawLocation in (Environment.GetEnvironmentVariable("IIO_WNCG_LOCATIONS") ?? "Canada,UnitedStates").Split(","))
{
    var normalizedRawLocation = rawLocation.Trim();
    if(Enum.TryParse(typeof(CountryId), normalizedRawLocation, true, out object? location) && location != null)
    {
        Console.WriteLine($"Location found: '{normalizedRawLocation}'");
        selectedLocations.Add((CountryId)location);
    }
    else
    {
        Console.WriteLine($"Failed to parse location: '{normalizedRawLocation}'");
    }
}
var selectedGroups = Environment.GetEnvironmentVariable("IIO_WNCG_GROUPS")?.Split(",").Select(g => g.Trim()) ?? Array.Empty<string>();

var destinationDirectoryPath = Environment.GetEnvironmentVariable("IIO_WNCG_DESTINATION_DIRECTORY_PATH") ?? ".";
var preferLeastLoaded = bool.TryParse(Environment.GetEnvironmentVariable("IIO_WNCG_PREFER_LEAST_LOADED_SERVERS") ?? "true", out bool parsedPreferLeastLoaded) && parsedPreferLeastLoaded;
string fileNameFormat = Environment.GetEnvironmentVariable("IIO_WNCG_FILE_NAME_FORMAT") ?? "wg{n}.conf";
var numberOfRequestedFiles = int.TryParse(Environment.GetEnvironmentVariable("IIO_WNCG_NUMBER_OF_REQUESTED_FILES"), out int parsedNumberOfGeneratedFiles)
    ? parsedNumberOfGeneratedFiles
    : 1;

var interfaceAddress = Environment.GetEnvironmentVariable("IIO_WNCG_INTERFACE_ADDRESS") ?? "10.5.0.2/32";
var interfaceDns = Environment.GetEnvironmentVariable("IIO_WNCG_INTERFACE_DNS") ?? "103.86.96.100, 103.86.99.100";
var interfaceDisableRoutes = bool.TryParse(Environment.GetEnvironmentVariable("IIO_WNCG_INTERFACE_DISABLE_ROUTE"), out bool parsedEnableRoutes) && parsedEnableRoutes;
var peerAllowedIps = Environment.GetEnvironmentVariable("IIO_WNCG_PEER_ALLOWED_IPS") ?? "0.0.0.0/0";
int peerPersistentKeepAlive = int.TryParse(Environment.GetEnvironmentVariable("IIO_WNCG_PEER_PERSISTENT_KEEP_ALIVE"), out int parsedPeerPersistentKeepAlive)
    ? parsedPeerPersistentKeepAlive
    : 25;
#endregion

Console.WriteLine($@"Configuration:
    IIO_WNCG_WIREGUARD_PRIVATE_KEY: {(nordVpnWireguardPrivateKey.Length > 5 ? nordVpnWireguardPrivateKey.Substring(0, 5) : "****")}...
    IIO_WNCG_LOCATIONS: {string.Join(", ", selectedLocations)}
    IIO_WNCG_GROUPS: {string.Join(", ", selectedGroups)}
    IIO_WNCG_DESTINATION_DIRECTORY_PATH: {destinationDirectoryPath}
    IIO_WNCG_PREFER_LEAST_LOADED_SERVERS: {preferLeastLoaded}
    IIO_WNCG_FILE_NAME_FORMAT: {fileNameFormat}
    IIO_WNCG_NUMBER_OF_REQUESTED_FILES: {numberOfRequestedFiles}
    IIO_WNCG_INTERFACE_ADDRESS: {interfaceAddress}
    IIO_WNCG_INTERFACE_DNS: {interfaceDns}
    IIO_WNCG_INTERFACE_DISABLE_ROUTE: {interfaceDisableRoutes}
    IIO_WNCG_PEER_ALLOWED_IPS: {peerAllowedIps}
    IIO_WNCG_PEER_PERSISTENT_KEEP_ALIVE: {peerPersistentKeepAlive}
");

var nordvpnClient = new NordVpnApiClient();

Console.WriteLine("Retrieving all NordVPN endpoints ...");
var allNordVpnEndpoints = await nordvpnClient.GetAllEndpointsAsync();

var onlineFilteredWireguardEndpoints = allNordVpnEndpoints
        .Where(e => e.Technologies.Any(t => t.Id == ProtocolId.Wireguard))
        .Where(e => e.Technologies.Any(t => t.Pivot.Status == Status.Online));

if (selectedGroups.Any())
{
    onlineFilteredWireguardEndpoints = onlineFilteredWireguardEndpoints.Where(e => e.Groups.Any(g => selectedGroups.Contains(g.Title)));
}

string selectedCountryDisplay = "all";
if (selectedLocations.Any())
{
    onlineFilteredWireguardEndpoints = onlineFilteredWireguardEndpoints.Where(e => e.Locations.Any(l => selectedLocations.Contains(l.Country.Id)));
    selectedCountryDisplay = string.Join(", ", selectedLocations.Select(l => l.ToString()));
}

if (!onlineFilteredWireguardEndpoints.Any())
{
    Console.Error.WriteLine($"Could not find any online wireguard endpoint in countries: {selectedCountryDisplay}");
    Environment.ExitCode = 1;
    return;
}

Console.WriteLine($"{onlineFilteredWireguardEndpoints.Count()} online wireguard endpoints found in countries: {selectedCountryDisplay}");

if (preferLeastLoaded)
{
    onlineFilteredWireguardEndpoints = onlineFilteredWireguardEndpoints.OrderBy(e => e.Load);
}

Console.WriteLine("Creating configuration files");
Directory.CreateDirectory(destinationDirectoryPath);

int generatedFileCount = 0;
foreach (var selectedEndpoint in onlineFilteredWireguardEndpoints)
{
    if (numberOfRequestedFiles != -1 && generatedFileCount >= numberOfRequestedFiles)
    {
        break;
    }
    var wireguardTechnologyInfo = selectedEndpoint.Technologies.FirstOrDefault(t => t.Id == ProtocolId.Wireguard);
    var wireguardPublicKey = wireguardTechnologyInfo?.Metadata.FirstOrDefault(m => m.Name == "public_key")?.Value;

    if (string.IsNullOrEmpty(wireguardPublicKey))
    {
        Console.WriteLine($"Ignoring endpoint, could not find public key ({selectedEndpoint.Name}");
        continue;
    }

    var fileContent = $@"
[Interface]
PrivateKey = {nordVpnWireguardPrivateKey}
ListenPort = 51820
Address = {interfaceAddress}
DNS = {interfaceDns}
Table = {(interfaceDisableRoutes ? "off" : "on")}

[Peer]
PublicKey = {wireguardPublicKey}
AllowedIPs = {peerAllowedIps}
Endpoint = {selectedEndpoint.Hostname}:51820
PersistentKeepalive = {peerPersistentKeepAlive}
";
    var endpointExtractedId = selectedEndpoint.Hostname.Replace(".nordvpn.com", "");
    var fileName = fileNameFormat
        .Replace("{n}", generatedFileCount.ToString())
        .Replace("{country}", selectedEndpoint.Locations.FirstOrDefault()?.Country?.Id.ToString() ?? "unknown")
        .Replace("{endpointId}", endpointExtractedId)
        .Replace("{load}", selectedEndpoint.Load.ToString());

    var destinationFilePath = Path.Combine(destinationDirectoryPath, fileName);
    File.WriteAllText(destinationFilePath, fileContent);
    Console.WriteLine(@$"----------
Generated file: {destinationFilePath}
    Name:           {selectedEndpoint.Name}
    Endpoint:       {selectedEndpoint.Hostname}
    Country:        {selectedEndpoint.Locations.FirstOrDefault()?.Country?.Id.ToString() ?? "unknown"}
    Load:           {selectedEndpoint.Load}
    Groups:         {string.Join(", ", selectedEndpoint.Groups.Select(g => g.Title))}
    Services:       {string.Join(", ", selectedEndpoint.Services.Select(s => s.Name))}
    Locations:      {string.Join(", ", selectedEndpoint.Locations.Select(l => l.Country.Name))}
");

    generatedFileCount++;
}
