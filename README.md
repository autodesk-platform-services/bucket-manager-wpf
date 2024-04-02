# Desktop tool to manage buckets and objects

It is a .NET 8 and WPF update for https://github.com/Autodesk-Forge/forge-bucketsmanager-desktop

It also uses the new Autodesk .Net SDKs.

# Demonstration

This sample is a Windows desktop application that lists all buckets for a given APS Client ID & Secret, allow creating new buckets, upload new files, translate and delete. Allow download of SVF for offline viewing. Simple JavaScript for testing code. It is intended as tool for developers.

 Setup

## Prerequisites

1. **Visual Studio 2022+**: This sample is windows only
2. **Webview2**: Chromium based browser control for .NET apps. It requires EDGE and WebView2 Chromium Runtime to run.
3. **.NET Framework** basic knowledge with C#

## Running locally

For using this sample, you need an Autodesk developer credentials. Visit the [Autodesk Platform Services](https://aps.autodesk.com/), sign up for an account, then [create an app](https://aps.autodesk.com/myapps/).

Download the repository, open `bucket.manager.wpf.sln` solution with Visual Studio. The build process should 
download the required packages (**Autodesk** and dependencies). Run the project. 

If you have set **APS_CLIENT_ID** and **APS_CLIENT_SECRET** in your system environment, you'll have the id and 
secret filled automatically. If you haven't set them, please enter your Client ID & 
Secret, click on **Authenticate** button. The app will obtain a 2-legged token and list buckets and files. After translating, files should be Viewable.

# Features

This sample app includes a few features:

## Download SVF

After creating a bucket and uploading an object, translate the file. When finish, open on the Viewer. Click on **Download SVF** button and select a destination folder. The HTML to view the file needs to point to the .svf file, usually a **0.SVF** under a folder with the same name of the viewable.

Note: SVF2 can't be downloaded.

## Running JavaScript code

The WebView2 control allow **.ExecuteScriptAsync()** for executing JavaScript code, which can be used for quick 
testing some code. Load a model on the Viewer, then click on **JavaScript** button. Type, paste or open a .js file, 
then run it with execute command in the right click context menu (or `Ctrl+E`), the result will be shown at the 
DevTools Console or browser window. 

This sample is licensed under the terms of the [MIT License](http://opensource.org/licenses/MIT). Please see the [LICENSE](LICENSE.txt) file for full details.
