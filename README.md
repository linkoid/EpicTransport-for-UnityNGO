# EpicTransport-for-UnityNGO
A trasport layer for Unity Netcode for Game Objects implemented using Epic Online Services' P2P Sockets

[GitHub](https://github.com/linkoid/EpicTransport-for-UnityNGO)

## Requirements
* Unity [Netcode for Game Objects](https://docs-multiplayer.unity3d.com/netcode/current/about/)
* [Epic Online Services C# SDK](https://dev.epicgames.com/en-US/sdk)

## Installing

### Intall via the Unity Package Manager
1. Open Package Manager window (Window > Package Manager)
1. Click `+` button on the upper-left of a window, and select "Add package from git URL..."
1. Enter the following URL and click `Add` button
```
https://github.com/linkoid/EpicTransport-for-UnityNGO.git?path=/netcode.transport.epic
```



## Setting up the Project
1. Add the Unity Netcode for Game Objects package.
	* In a Unity Project, go to (Window > Package Manager), then (Packages > Unity Registry), select "Netcode for Game Objects", and install.
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
1. Continue following the steps for creating the EOSSDKComponent and Initializing the SDK on the [EOS C# Example Documentation](https://dev.epicgames.com/docs/epic-online-services/eos-get-started/working-with-the-eos-sdk/eossdkc-sharp-getting-started#integration).
	* The EOS documentation might show a different folder structure, but keep the folder structure specified in this README.
	* A simple but complete implementation of the EOSSDKComponent can be copied from the [EpicTransportExample project](TODO).
1. 



## Usage
This reposity only provides the transport layer that uses Epic Online Services.
The EOS SDK needs to be intialized before attempting to use Epic Transport.

Use Epic's FREE Online Services as a transport layer for Unity's Netcode for Game Objects. 

## FAQs







