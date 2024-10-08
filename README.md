# IzireIO.WireguardNordVpnConfGenerator
A simple program to generate wireguard configuration files using NordVPN API.

## Configuration

### Get NordVPN API Credentials

Reference: https://gist.github.com/bluewalk/7b3db071c488c82c604baf76a42eaad3

1. [Install Nord VPN on Linux](https://support.nordvpn.com/hc/en-us/articles/20196094470929-Installing-NordVPN-on-Linux-distributions)

    ```bash
    sh <(wget -qO - https://downloads.nordcdn.com/apps/linux/install.sh)
    ```

    As root:

    ```bash
    groupadd nordvpn
    usermod -aG nordvpn $USER
    ```

    where $USER is your non-root username.

    Restart your computer:

    ```bash
    shutdown -r now
    ```

2. Login to NordVPN:

    ```bash
    nordvpn login
    ```

3. Install wireguard

    ```bash
    sudo apt-get install wireguard-tools
    ```
4. Get the NordVPN private key

    ```bash
    sudo wg show nordlynx private-key
    ```

### Environment Variables

| **Name**                                             | **Description**                                                                                                                                                                                                                                                                                                                                                                                 |
|------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `IIO_WNCG_WIREGUARD_PRIVATE_KEY`                     | (**Mandatory**) string: Base64 encoded           <br> Peer private key. Can be obtained using few CLI commands in a Linux environment.<br>See document: https://gist.github.com/bluewalk/7b3db071c488c82c604baf76a42eaad3                                                                                                                                                                       |
| `IIO_WNCG_PRE_RESOLVE_HOSTNAME_DURING_CONF_CREATION` | (Opt) string, default: false <br> Resolve NordVPN DNS during configuration file creation. Then use IP instead of FQDN to generate file                                                                                                                                                                                                                                                          |
| `IIO_WNCG_LOCATIONS`                                 | (Opt) string, default: None (comma separated values)   <br> Country name(s) to filter selected vpn servers (ex: `Canada, UnitedStates`)                                                                                                                                                                                                                                                         |
| `IIO_WNCG_PREFER_LEAST_LOADED_SERVERS`               | (Opt) boolean, default: `true`                        <br> If true, we will sort vpn servers by load in an ascending manner.<br>This is to select least loaded servers first.                                                                                                                                                                                                                   |
| `IIO_WNCG_DESTINATION_DIRECTORY_PATH`                | (Opt) string, default: `.`                            <br> Destination directory for generated files                                                                                                                                                                                                                                                                                            |
| `IIO_WNCG_NUMBER_OF_REQUESTED_FILES`                 | (Opt) number, default: `1`                            <br> Max number of file to generate. `-1` to generate all files.                                                                                                                                                                                                                                                                          |
| `IIO_WNCG_INTERFACE_ADDRESS`                         | (Opt) string, default: `10.5.0.2/32`                  <br> Wireguard interface address                                                                                                                                                                                                                                                                                                          |
| `IIO_WNCG_INTERFACE_DNS`                             | (Opt) string, default: `103.86.96.100, 103.86.99.100` <br> Wireguard interface DNS, by default uses NordVpn's                                                                                                                                                                                                                                                                                   |
| `IIO_WNCG_INTERFACE_DISABLE_ROUTE`                   | (Opt) boolean, default: `false` <br> If `true`, add `Table = off` to the `[Interface]` section to disable installation of routes upon wireguard connection.                                                                                                                                                                                                                                     |
| `IIO_WNCG_PEER_ALLOWED_IPS`                          | (Opt) string, default: `0.0.0.0/0`                    <br> IP ranges to go through the VPN, by default: everything.<br>See: https://www.procustodibus.com/blog/2021/03/wireguard-allowedips-calculator/                                                                                                                                                                                         |
| `IIO_WNCG_FILE_NAME_FORMAT`                          | (Opt) string, default: `wg{n}.conf`                   <br> File name format to use for generated files. <br>Placeholders:<br>- `{n}`: replaced by generated file index (0 to n)<br>- `{country}`: replaced by country name<br>- `{endpointId`: replaced by the subdomain identifying the endpoint (ex: `us123` for `us123.nordvpn.com`)<br>- `{load}`:  current endpoint load (1 to 100)        |
| `IIO_WNCG_PEER_PERSISTENT_KEEP_ALIVE`                | (Opt) number, default: `25`                           <br> Peer keep alive property                                                                                                                                                                                                                                                                                                             |
| `IIO_WNCG_GROUPS`                                    | (Opt) string, default: None (comma separated values)  <br> Groups to filter selected vpn servers (values: `Standard VPN servers`,`P2P`,`Europe`,`The Americas`,`Dedicated IP`,`Asia Pacific`,`Obfuscated Servers`,`Africa`,`the Middle East and India`,`Onion Over VPN`,`Double VPN`)                                                                                                           |

