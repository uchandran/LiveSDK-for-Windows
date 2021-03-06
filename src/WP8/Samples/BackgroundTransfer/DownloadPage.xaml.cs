﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using Microsoft.Phone.Controls;

namespace Microsoft.Live.WP8.Samples.BackgroundTransfer
{
    public partial class DownloadPage : PhoneApplicationPage, IProgress<LiveOperationProgress>
    {
        private string currentFolderId;
        private readonly Stack<string> folderIdStack; 
        private readonly LiveConnectClient connectClient;
        private CancellationTokenSource cts;

        public DownloadPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            this.folderIdStack = new Stack<string>();
            this.connectClient = new LiveConnectClient(((App) App.Current).Session);
        }

        public void Report(LiveOperationProgress value)
        {
            this.DownloadProgressBar.Value = value.ProgressPercentage;
        }

        protected override void OnBackKeyPress(CancelEventArgs e)
        {
            if (this.folderIdStack.Count > 0)
            {
                this.SkyDriveItemList.ItemsSource = null;

                string folderId = this.folderIdStack.Pop();
                this.currentFolderId = folderId;
                this.LoadSkyDriveItemList(folderId + "/files");

                e.Cancel = true;
            }
        }

        private void PrepareForDownload()
        {
            this.CancelButton.IsEnabled = true;
            this.DownloadButton.IsEnabled = false;
            this.DownloadProgressBar.Value = 0;
            this.cts = new CancellationTokenSource();
        }

        private void CleanUpAfterDownload()
        {
            this.CancelButton.IsEnabled = false;
            this.DownloadButton.IsEnabled = true;
        }

        private async void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            // Look for any previously existing BackgroundDownloads.
            // This must be called to clear old requests out of the system.
            foreach (LivePendingDownload pendingDownload in this.connectClient.GetPendingBackgroundDownloads())
            {
                this.PrepareForDownload();

                try
                {
                    LiveOperationResult operationResult = await pendingDownload.AttachAsync(this.cts.Token, this);
                    dynamic result = operationResult.Result;
                    MessageBox.Show("Downloaded to " + result.downloadLocation);
                } 
                catch (TaskCanceledException)
                {
                    MessageBox.Show("Download canceled");
                }

                this.CleanUpAfterDownload();
            }

            this.currentFolderId = "me/skydrive";
            this.LoadSkyDriveItemList("me/skydrive/files");
        }

        private void SkyDriveItemList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var skydriveItem = this.SkyDriveItemList.SelectedItem as SkyDriveItem;
            if (skydriveItem == null)
            {
                return;
            }

            switch (skydriveItem.Type)
            {
                case "album":
                case "folder":
                    this.DownloadButton.IsEnabled = false;
                    this.SkyDriveItemList.ItemsSource = null;

                    this.folderIdStack.Push(this.currentFolderId);
                    this.currentFolderId = skydriveItem.Id;

                    this.LoadSkyDriveItemList(skydriveItem.Id + "/files");
                    break;
                default:
                    this.DownloadButton.IsEnabled = true;
                    break;
            }
        }

        private async void LoadSkyDriveItemList(string path)
        {
            LiveOperationResult result = await this.connectClient.GetAsync(path);

            dynamic dynamicResult = result.Result;
            var skydriveItems = new List<SkyDriveItem>();

            foreach (dynamic item in dynamicResult.data)
            {
                skydriveItems.Add(new SkyDriveItem(item));
            }

            this.SkyDriveItemList.ItemsSource = skydriveItems;
        }

        private void SetBackgroundTransferPreferences()
        {
            var selectedBackgroundTransferPreference =
                this.BackgroundTransferPreferencesListBox.SelectedItem as ListBoxItem;
            BackgroundTransferPreferences bgTransferPrefs;

            Debug.Assert(Enum.TryParse(selectedBackgroundTransferPreference.Content.ToString(), out bgTransferPrefs));

            this.connectClient.BackgroundTransferPreferences = bgTransferPrefs;
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            this.SetBackgroundTransferPreferences();

            var skydriveItem = this.SkyDriveItemList.SelectedItem as SkyDriveItem;
            string path = skydriveItem.Id + "/content";
            var downloadLocation = new Uri("/shared/transfers/" + skydriveItem.Name, UriKind.RelativeOrAbsolute);

            this.PrepareForDownload();

            try
            {
                LiveOperationResult operationResult =
                    await this.connectClient.BackgroundDownloadAsync(path, downloadLocation, this.cts.Token, this);
                dynamic result = operationResult.Result;
                MessageBox.Show("Downloaded to " + result.downloadLocation);
            } 
            catch (TaskCanceledException)
            {
                MessageBox.Show("Download canceled");
            }

            this.CleanUpAfterDownload();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.cts.Cancel();
        }
    }
}