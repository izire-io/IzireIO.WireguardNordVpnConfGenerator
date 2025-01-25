using IzireIO.NordVpn.Api.Client;
using IzireIO.NordVpn.Api.Client.Enum;
using System.Net;

#region default vars
var nordVpnWireguardPrivateKey = "";
var preResolveHostname = false;
var selectedLocations = new List<CountryId>() { CountryId.Japan };
var selectedGroups = new List<string>();
var destinationDirectoryPath = ".";
var preferLeastLoaded = true;
var fileNameFormat = "wg{n}.conf";
var numberOfRequestedFiles = 1;
var interfaceAddress = "10.5.0.2/32";
var interfaceDns = "103.86.96.100, 103.86.99.100";
var interfaceDisableRoutes = false;
var peerAllowedIps = "0.0.0.0/0";
var peerPersistentKeepAlive = 25;
#endregion

#region Parameter retrieval
nordVpnWireguardPrivateKey = Environment.GetEnvironmentVariable("IIO_WNCG_WIREGUARD_PRIVATE_KEY") ?? nordVpnWireguardPrivateKey;
if (string.IsNullOrEmpty(nordVpnWireguardPrivateKey))
{
    Console.Error.WriteLine("Missing IIO_WNCG_WIREGUARD_PRIVATE_KEY environment variable.");
    Environment.ExitCode = 1;
    return;
}

preResolveHostname = bool.TryParse(Environment.GetEnvironmentVariable("IIO_WNCG_PRE_RESOLVE_HOSTNAME_DURING_CONF_CREATION") ?? "false", out bool parsedUseDirectIp) && parsedUseDirectIp;

if (Environment.GetEnvironmentVariable("IIO_WNCG_LOCATIONS") != null)
{
    selectedLocations.Clear();
    foreach (var rawLocation in (Environment.GetEnvironmentVariable("IIO_WNCG_LOCATIONS")?.Split(",") ?? []))
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
}
selectedGroups = Environment.GetEnvironmentVariable("IIO_WNCG_GROUPS")?.Split(",").Select(g => g.Trim()).ToList() ?? selectedGroups;

destinationDirectoryPath = Environment.GetEnvironmentVariable("IIO_WNCG_DESTINATION_DIRECTORY_PATH") ?? destinationDirectoryPath;
preferLeastLoaded = bool.TryParse(Environment.GetEnvironmentVariable("IIO_WNCG_PREFER_LEAST_LOADED_SERVERS") ?? "true", out bool parsedPreferLeastLoaded) && parsedPreferLeastLoaded;
fileNameFormat = Environment.GetEnvironmentVariable("IIO_WNCG_FILE_NAME_FORMAT") ?? fileNameFormat;
numberOfRequestedFiles = int.TryParse(Environment.GetEnvironmentVariable("IIO_WNCG_NUMBER_OF_REQUESTED_FILES"), out int parsedNumberOfGeneratedFiles)
    ? parsedNumberOfGeneratedFiles
    : numberOfRequestedFiles;

interfaceAddress = Environment.GetEnvironmentVariable("IIO_WNCG_INTERFACE_ADDRESS") ?? interfaceAddress;
interfaceDns = Environment.GetEnvironmentVariable("IIO_WNCG_INTERFACE_DNS") ?? interfaceDns;
interfaceDisableRoutes = bool.TryParse(Environment.GetEnvironmentVariable("IIO_WNCG_INTERFACE_DISABLE_ROUTE"), out bool parsedEnableRoutes) && parsedEnableRoutes;
peerAllowedIps = Environment.GetEnvironmentVariable("IIO_WNCG_PEER_ALLOWED_IPS") ?? peerAllowedIps;
peerPersistentKeepAlive = int.TryParse(Environment.GetEnvironmentVariable("IIO_WNCG_PEER_PERSISTENT_KEEP_ALIVE"), out int parsedPeerPersistentKeepAlive)
    ? peerPersistentKeepAlive
    : 25;
#endregion

Console.WriteLine($@"Configuration:
    IIO_WNCG_PRE_RESOLVE_HOSTNAME_DURING_CONF_CREATION: {preResolveHostname}
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

    var hostname = preResolveHostname
        ? Dns.GetHostEntry(selectedEndpoint.Hostname).AddressList.First().ToString()
        : selectedEndpoint.Hostname;

    var fileContent = $@"
[Interface]
PrivateKey = {nordVpnWireguardPrivateKey}
ListenPort = 51820
Address = {interfaceAddress}
DNS = {interfaceDns}
{(interfaceDisableRoutes ? "Table = off" : "")}

[Peer]
PublicKey = {wireguardPublicKey}
AllowedIPs = {peerAllowedIps}
Endpoint = {hostname}:51820
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
    Endpoint:       {hostname} {(preResolveHostname ? $"(Resolved now from {selectedEndpoint.Hostname}" : "")}
    Country:        {selectedEndpoint.Locations.FirstOrDefault()?.Country?.Id.ToString() ?? "unknown"}
    Load:           {selectedEndpoint.Load}
    Groups:         {string.Join(", ", selectedEndpoint.Groups.Select(g => g.Title))}
    Services:       {string.Join(", ", selectedEndpoint.Services.Select(s => s.Name))}
    Locations:      {string.Join(", ", selectedEndpoint.Locations.Select(l => l.Country.Name))}
");

    generatedFileCount++;
}
