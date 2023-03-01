# Desktop tool to manage buckets and objects

It is a .NET 6 and WPF update for https://github.com/Autodesk-Forge/forge-bucketsmanager-desktop

# Demonstration

This sample is a Windows desktop application that lists all buckets for a given Forge Client ID & Secret, allow creating new buckets, upload new files, translate and delete. Allow download of SVF for offline viewing. Simple JavaScript for testing code. It is intended as tool for developers.

 Setup

## Prerequisites

1. **Visual Studio**: Either Community (Windows) or Code (Windows, MacOS).
2. **Webview2**: Chromium based browser control for .NET apps. It requires EDGE and WebView2 Chromium Runtime to run.
3. **.NET Framework** basic knowledge with C#

## Running locally

For using this sample, you need an Autodesk developer credentials. Visit the [Forge Developer Portal](https://developer.autodesk.com), sign up for an account, then [create an app](https://developer.autodesk.com/myapps/create).

Download the repository, open `bucket.manager.sln` Solution on Visual Studio. The build process should download the required packages (**Autodesk.Forge** and dependencies). Run the project. At the form, enter your Client ID & Secret, click on **Authenticate** button. The app will obtain a 2-legged token and list buckets and files. After translating, files should be Viewable.

# Features

This sample app includes a few features:

## Download SVF

After creating a bucket and uploading an object, translate the file. When finish, open on the Viewer. Click on **Download SVF** button and select a destination folder. The HTML to view the file needs to point to the .svf file, usually a **0.SVF** under a folder with the same name of the viewable.

## Running JavaScript code

The WebView2 control allow **.ExecuteScriptAsync()** for executing JavaScript code, which can be used for quick testing some code. Load a model on the Viewer, then click on **JavaScript** button. Type, paste or open a .js file, then click on **Run** (or `Ctrl+R`) to run, the result will show on the bottom text area and (if applicable) at the DevTools Console). The video demonstrate it:

This sample is licensed under the terms of the [MIT License](http://opensource.org/licenses/MIT). Please see the [LICENSE](LICENSE.txt) file for full details.
