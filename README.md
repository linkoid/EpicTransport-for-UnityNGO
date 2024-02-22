# Epic Transport for Unity NGO
A trasport layer for Unity Netcode for Game Objects implemented using Epic Online Services' P2P Sockets

[GitHub](https://github.com/linkoid/EpicTransport-for-UnityNGO)

## Requirements
* Unity [Netcode for Game Objects](https://docs-multiplayer.unity3d.com/netcode/current/about/)
* [Epic Online Services C# SDK](https://dev.epicgames.com/en-US/sdk)

## Installing

### Intall via the Unity Package Manager
1. Open Package Manager window (Window > Package Manager)
1. Click the `+` button in the upper-left of the window, and select "Add package from git URL..."
1. Enter the following URL and click `Add` button
```
https://github.com/linkoid/EpicTransport-for-UnityNGO.git?path=/netcode.transport.epic
```

## Usage

### Setting up the Project
This reposity only provides the transport layer that uses Epic Online Services.

The EOS SDK will need to be downloaded and set up manually.

1. Install the EpicTransport-for-UnityNGO package (see above).
1. Add the EOS C# SDK to the project.
	* Download the [EOS CSharp SDK](https://dev.epicgames.com/en-US/sdk). (Make sure to select C# from the dropdown.)
	* Unzip the downloaded zip file.
	* Create a new folder in the project's Assets folder named Plugins, and then a new folder named EOSSDK inside of the Plugins folder.
	* Copy the SDK/Bin folder and SDK/Source folder from the unzipped folder into Assets/Plugins/EOSSDK/Bin and Assets/Plugins/EOSSDK/Sourc
1. Create an assenbly definition for the EOSSDK named Epic.OnlineServices.
	* Inside the Assets/Plugins/EOSSDK folder, (Right click > Create > Assembly Definition).
	* Rename the newly created assembly definition file to "Epic.OnlineServices".
1. Delete files that won't be used on your platform from the Assets/Plugins/EOSSDK/Bin folder.
	* For Widows, you would delete everything in the Bin folder except `EOSSDK-Win64-Shipping.dll`.
	* If you intend to support multiple platforms later, you can read about configuring plugin platforms here: [Import and configure plug-ins](https://docs.unity3d.com/Manual/PluginInspector.html).
1. Continue following the steps for creating the `EOSSDKComponent` and Initializing the SDK on the [EOS C# Example Documentation](https://dev.epicgames.com/docs/epic-online-services/eos-get-started/working-with-the-eos-sdk/eossdkc-sharp-getting-started#integration).
	* The EOS documentation might show a different folder structure, but keep the folder structure specified in this README.
	* A simple but complete implementation of the `EOSSDKComponent` can be copied from the [Epic Transport Example Project](TODO).

### Setting up Epic Transport
The EpicTransport component works with the NetworkManager component.
The EOS SDK needs to be intialized and a user must be logged in before attempting to use EpicTransport.
You can find an example of this process in the [Epic Transport Example Project](TODO).

1. Setup a NetworkManager to use EpicTransport.
	* Create a new game object and add a NetworkManager component to it.
	* In the NetworkManager component, there should be a "select transport" dropdown.
	Select `EpicTransport` from that dropdown.
1. Set the `PlatforInterface` and `LocalUserId` of the EpicTransport component after the user has logged in.
	* There are multiple different ways of doing this, but it is recomended to use the `IPlatformInterfaceProvider` and `ILocalUserIdProvider`
	* A good place to implement these interfaces is on the `EOSSDKComponent`. An example of this can be seen in the [Epic Transport Example Project](TODO).
1. Set the `HostUserId` of EpicTransport if you are connecting to a host.
	* This will most likey have to be done using a string, in which case use `EpicTransport.SetHost()`.
1. Finally, start the NetworkManager as normal.

