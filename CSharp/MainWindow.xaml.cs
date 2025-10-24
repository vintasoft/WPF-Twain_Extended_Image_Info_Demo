using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Vintasoft.WpfTwain;

namespace WpfTwainExtendedImageInfoDemo
{
    public partial class MainWindow : Window
    {

        #region Fields

        /// <summary>
        /// TWAIN device manager.
        /// </summary>
        DeviceManager _deviceManager;

        /// <summary>
        /// Current device.
        /// </summary>
        Device _currentDevice;

        #endregion



        #region Constructor

        public MainWindow()
        {
            InitializeComponent();

            this.Title = String.Format("VintaSoft WPF TWAIN Extended Image Info Demo v{0}", TwainEnvironment.ProductVersion);

            // create instance of the DeviceManager class
            _deviceManager = new DeviceManager(this);
        }

        #endregion



        #region Methods

        private void Window_Loaded(object sender, EventArgs e)
        {
            //
            string[] extendedImageInfoNames = Enum.GetNames(typeof(ExtendedImageInfoId));
            //
            CheckBox checkBox;
            for (int i = 0; i < extendedImageInfoNames.Length; i++)
            {
                checkBox = new CheckBox();
                checkBox.Content = extendedImageInfoNames[i].Replace('_', ' ');

                extendedImageInfoToRetrievePanel.Children.Add(checkBox);
            }

            // select the standard extended image infos
            SelectStandardExtendedImageInfos();

            // open TWAIN device manager
            OpenDeviceManager();
        }

        /// <summary>
        /// Sets form's UI state.
        /// </summary>
        private void SetFormUiState(bool enabled)
        {
            acquireImageButton.IsEnabled = enabled;
        }


        /// <summary>
        /// Opens TWAIN device manager.
        /// </summary>
        private bool OpenDeviceManager()
        {
            SetFormUiState(false);

            try
            {
                // get path to the TWAIN device manager 2.x from installation of VintaSoft TWAIN .NET SDK
                string twainDsmDllCustomPath = GetTwainDsmCustomPath(IntPtr.Size == 4);
                // if file exist
                if (twainDsmDllCustomPath != null)
                    // specify that SDK should use TWAIN device manager 2.x from installation of VintaSoft TWAIN .NET SDK
                    _deviceManager.TwainDllPath = twainDsmDllCustomPath;

                // try to find the TWAIN device manager 2.x
                _deviceManager.IsTwain2Compatible = true;

                // if TWAIN device manager 2.x is not available
                if (!_deviceManager.IsTwainAvailable)
                {
                    // try to use TWAIN device manager 1.x
                    _deviceManager.IsTwain2Compatible = false;
                    // if TWAIN device manager 1.x is not available
                    if (!_deviceManager.IsTwainAvailable)
                    {
                        MessageBox.Show("TWAIN device manager is not found.");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                // show dialog with error message
                MessageBox.Show(GetFullExceptionMessage(ex), "TWAIN device manager", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // if 64-bit TWAIN2 device manager is used
            if (IntPtr.Size == 8 && _deviceManager.IsTwain2Compatible)
            {
                if (!InitTwain2DeviceManagerMode())
                {
                    MessageBox.Show("TWAIN device manager is not opened.");
                    return false;
                }
            }

            try
            {
                // open the device manager
                _deviceManager.Open();
            }
            catch (Exception ex)
            {
                // show dialog with error message
                MessageBox.Show(GetFullExceptionMessage(ex), "TWAIN device manager", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // if no devices are found in the system
            if (_deviceManager.Devices.Count == 0)
            {
                MessageBox.Show("No devices found.");
                return false;
            }

            SetFormUiState(true);
            return true;
        }

        /// <summary>
        /// Initializes the device manager mode.
        /// </summary>
        private bool InitTwain2DeviceManagerMode()
        {
            // create a window that allows to view and edit mode of 64-bit TWAIN2 device manager
            SelectDeviceManagerModeWindow window = new SelectDeviceManagerModeWindow();
            // initialize window
            window.Owner = this;
            window.Use32BitDevices = _deviceManager.Are32BitDevicesUsed;

            // show dialog
            if (window.ShowDialog() == true)
            {
                // get path to the TWAIN device manager 2.x from installation of VintaSoft TWAIN .NET SDK
                string twainDsmDllCustomPath = GetTwainDsmCustomPath(window.Use32BitDevices);
                // if file exist
                if (twainDsmDllCustomPath != null)
                    // specify that SDK should use TWAIN device manager 2.x from installation of VintaSoft TWAIN .NET SDK
                    _deviceManager.TwainDllPath = twainDsmDllCustomPath;

                // if device manager mode is changed
                if (window.Use32BitDevices != _deviceManager.Are32BitDevicesUsed)
                {
                    try
                    {
                        // if 32-bit devices must be used
                        if (window.Use32BitDevices)
                            _deviceManager.Use32BitDevices();
                        else
                            _deviceManager.Use64BitDevices();
                    }
                    catch (TwainDeviceManagerException ex)
                    {
                        // show dialog with error message
                        MessageBox.Show(GetFullExceptionMessage(ex), "TWAIN device manager", MessageBoxButton.OK, MessageBoxImage.Error);

                        return false;
                    }
                }
            }
            else
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Acquire image.
        /// </summary>
        private void acquireImageButton_Click(object sender, EventArgs e)
        {
            acquireImageButton.IsEnabled = false;
            try
            {
                // select the device
                if (!_deviceManager.ShowDefaultDeviceSelectionDialog())
                {
                    MessageBox.Show("Device is not selected.");
                    acquireImageButton.IsEnabled = true;
                    return;
                }

                if (_currentDevice != null)
                    UnsubscribeFromDeviceEvents();

                // get a reference to the selected device
                Device device = _deviceManager.DefaultDevice;

                _currentDevice = device;
                // subscribe to the device events
                SubscribeToDeviceEvents();

                // set acquisition parameters
                device.ShowUI = false;
                device.DisableAfterAcquire = true;

                // open the device
                device.Open();

                // determine if device supports the extended image info
                DeviceCapability extendedImageInfoCap = device.Capabilities.Find(DeviceCapabilityId.IExtImageInfo);
                if (extendedImageInfoCap == null)
                {
                    // close the device
                    device.Close();
                    MessageBox.Show("Device does not support extended image information.");
                    acquireImageButton.IsEnabled = true;
                    return;
                }

                // specify that image info is necessary
                AddExtendedImageInfoToRetrieveList(device);

                // start the asynchronous image acquisition process 
                device.Acquire();
            }
            catch (TwainException ex)
            {
                MessageBox.Show(GetFullExceptionMessage(ex), "Error");
                acquireImageButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// Subscribes to the device events.
        /// </summary>
        private void SubscribeToDeviceEvents()
        {
            _currentDevice.ImageAcquired += new EventHandler<ImageAcquiredEventArgs>(device_ImageAcquired);
            _currentDevice.ScanCanceled += new EventHandler(device_ScanCanceled);
            _currentDevice.ScanFailed += new EventHandler<ScanFailedEventArgs>(device_ScanFailed);
            _currentDevice.ScanFinished += new EventHandler(_currentDevice_ScanFinished);
        }

        /// <summary>
        /// Unsubscribes from the device events.
        /// </summary>
        private void UnsubscribeFromDeviceEvents()
        {
            _currentDevice.ImageAcquired -= new EventHandler<ImageAcquiredEventArgs>(device_ImageAcquired);
            _currentDevice.ScanCanceled -= new EventHandler(device_ScanCanceled);
            _currentDevice.ScanFailed -= new EventHandler<ScanFailedEventArgs>(device_ScanFailed);
            _currentDevice.ScanFinished -= new EventHandler(_currentDevice_ScanFinished);
        }

        /// <summary>
        /// Image is acquired.
        /// </summary>
        private void device_ImageAcquired(object sender, ImageAcquiredEventArgs e)
        {
            // dispose an acquired image
            e.Image.Dispose();

            // output an extended image info

            extendedImageInfoAboutAcquiredImageTextBox.Text += "IMAGE IS ACQUIRED" + Environment.NewLine;
            extendedImageInfoAboutAcquiredImageTextBox.Text += Environment.NewLine;

            Device device = (Device)sender;
            for (int i = 0; i < device.ExtendedImageInfo.Count; i++)
            {
                AddExtendedImageInfoToResultTextBox(i, device.ExtendedImageInfo[i]);
            }
            extendedImageInfoAboutAcquiredImageTextBox.Text += Environment.NewLine;
        }

        /// <summary>
        /// Scan is canceled.
        /// </summary>
        private void device_ScanCanceled(object sender, EventArgs e)
        {
            MessageBox.Show("Scan is canceled.");
        }

        /// <summary>
        /// Scan is failed.
        /// </summary>
        private void device_ScanFailed(object sender, ScanFailedEventArgs e)
        {
            MessageBox.Show(e.ErrorString, "Scan is failed");
        }

        /// <summary>
        /// Scan is finished.
        /// </summary>
        void _currentDevice_ScanFinished(object sender, EventArgs e)
        {
            // close the device
            _currentDevice.Close();

            acquireImageButton.IsEnabled = true;
        }


        /// <summary>
        /// Select/unselect all types of extended image info.
        /// </summary>
        private void selectAllExtendedImageInfoButton_Click(object sender, EventArgs e)
        {
            bool selectAll = false;

            if ((string)selectAllExtendedImageInfoButton.Content == "Select all")
            {
                selectAll = true;
                selectAllExtendedImageInfoButton.Content = "Unselect all";
            }
            else
            {
                selectAllExtendedImageInfoButton.Content = "Select all";
            }

            CheckBox checkBox;
            for (int i = 0; i < extendedImageInfoToRetrievePanel.Children.Count; i++)
            {
                checkBox = (CheckBox)extendedImageInfoToRetrievePanel.Children[i];
                checkBox.IsChecked = selectAll;
            }
        }

        /// <summary>
        /// Select standard extended image infos (standard extended image infos always available
        /// if DeviceCapabilityId.IExtImageInfo capability is supported by device).
        /// </summary>
        private void SelectStandardExtendedImageInfos()
        {
            ExtendedImageInfoId[] standardExtendedImageInfoIds = new ExtendedImageInfoId[6] {
                ExtendedImageInfoId.DocumentNumber, ExtendedImageInfoId.PageNumber,
                ExtendedImageInfoId.Camera, ExtendedImageInfoId.FrameNumber,
                ExtendedImageInfoId.Frame, ExtendedImageInfoId.PixelFlavor };

            bool isStandardExtendedImageInfoFound;
            CheckBox checkBox;
            for (int i = 0; i < extendedImageInfoToRetrievePanel.Children.Count; i++)
            {
                checkBox = (CheckBox)extendedImageInfoToRetrievePanel.Children[i];
                string extendedImageInfoIdString = ((string)checkBox.Content).Replace(' ', '_');
                ExtendedImageInfoId extendedImageInfoId = (ExtendedImageInfoId)Enum.Parse(typeof(ExtendedImageInfoId), extendedImageInfoIdString);

                isStandardExtendedImageInfoFound = false;
                for (int j = 0; j < standardExtendedImageInfoIds.Length; j++)
                {
                    if (extendedImageInfoId == standardExtendedImageInfoIds[j])
                    {
                        isStandardExtendedImageInfoFound = true;
                        break;
                    }
                }

                checkBox.IsChecked = isStandardExtendedImageInfoFound;
            }
        }

        /// <summary>
        /// Add type of extended image info to the list of necessary extended image infos.
        /// </summary>
        private void AddExtendedImageInfoToRetrieveList(Device device)
        {
            device.ExtendedImageInfo.Clear();

            CheckBox checkBox;
            for (int i = 0; i < extendedImageInfoToRetrievePanel.Children.Count; i++)
            {
                checkBox = (CheckBox)extendedImageInfoToRetrievePanel.Children[i];
                if (checkBox.IsChecked == true)
                {
                    Type enumType = typeof(ExtendedImageInfoId);
                    string extendedImageInfoIdString = ((string)checkBox.Content).Replace(' ', '_');
                    ExtendedImageInfoId extendedImageInfoId = (ExtendedImageInfoId)Enum.Parse(enumType, extendedImageInfoIdString);

                    device.ExtendedImageInfo.Add(new ExtendedImageInfo(extendedImageInfoId));
                }
            }
        }

        /// <summary>
        /// Add an extended image info to the result.
        /// </summary>
        private void AddExtendedImageInfoToResultTextBox(int index, ExtendedImageInfo info)
        {
            if (!info.IsValueValid)
                return;

            extendedImageInfoAboutAcquiredImageTextBox.Text += string.Format("Extended image info {0}", index);
            extendedImageInfoAboutAcquiredImageTextBox.Text += Environment.NewLine;

            extendedImageInfoAboutAcquiredImageTextBox.Text += string.Format("  Name={0}", Enum.GetName(typeof(ExtendedImageInfoId), info.InfoId));
            extendedImageInfoAboutAcquiredImageTextBox.Text += Environment.NewLine;

            extendedImageInfoAboutAcquiredImageTextBox.Text += string.Format("  Id={0}", info.InfoId);
            extendedImageInfoAboutAcquiredImageTextBox.Text += Environment.NewLine;

            extendedImageInfoAboutAcquiredImageTextBox.Text += string.Format("  Value type={0}", info.ValueType);
            extendedImageInfoAboutAcquiredImageTextBox.Text += Environment.NewLine;

            TwainOneValueContainer oneDeviceCapabilityValue = info.Value as TwainOneValueContainer;
            if (oneDeviceCapabilityValue != null)
            {
                extendedImageInfoAboutAcquiredImageTextBox.Text += string.Format("  Value={0}", oneDeviceCapabilityValue.Value);
                extendedImageInfoAboutAcquiredImageTextBox.Text += Environment.NewLine;
            }
            else
            {
                TwainArrayValueContainer arrayDeviceCapabilityValue = info.Value as TwainArrayValueContainer;
                if (arrayDeviceCapabilityValue != null)
                {
                    extendedImageInfoAboutAcquiredImageTextBox.Text += "Values: ";
                    if (arrayDeviceCapabilityValue.Values != null)
                    {
                        if (arrayDeviceCapabilityValue.Values.GetType() == typeof(byte[]))
                        {
                            extendedImageInfoAboutAcquiredImageTextBox.Text += string.Format("byte[{0}]", arrayDeviceCapabilityValue.Values.Length);
                        }
                        else
                        {
                            for (int i = 0; i < arrayDeviceCapabilityValue.Values.Length; i++)
                                extendedImageInfoAboutAcquiredImageTextBox.Text += string.Format("{0}, ", arrayDeviceCapabilityValue.Values.GetValue(i));
                        }
                    }
                    extendedImageInfoAboutAcquiredImageTextBox.Text += Environment.NewLine;
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_currentDevice != null)
            {
                UnsubscribeFromDeviceEvents();
                _currentDevice = null;
            }

            // close the device manager
            _deviceManager.Close();
            // dispose the device manager
            _deviceManager.Dispose();
            _deviceManager = null;
        }

        /// <summary>
        /// Returns the message of exception and inner exceptions.
        /// </summary>
        private string GetFullExceptionMessage(System.Exception ex)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine(ex.Message);

            System.Exception innerException = ex.InnerException;
            while (innerException != null)
            {
                if (ex.Message != innerException.Message)
                    sb.AppendLine(string.Format("Inner exception: {0}", innerException.Message));
                innerException = innerException.InnerException;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns path to the TWAIN device manager 2.x from installation of VintaSoft TWAIN .NET SDK.
        /// </summary>
        /// <param name="use32BitDevice">The value indicating whether the 32-bit TWAIN device must be used.</param>
        /// <returns>The path to the TWAIN device manager 2.x from installation of VintaSoft TWAIN .NET SDK.</returns>
        private string GetTwainDsmCustomPath(bool use32BitDevice)
        {
            string twainFolderName = "TWAINDSM64";
            if (use32BitDevice)
                twainFolderName = "TWAINDSM32";

            string[] binFolderPaths = { @"..\..\Bin", @"..\..\..\..\..\Bin", @"..\..\..\..\..\..\Bin" };
            string binFolderPath = null;
            for (int i = 0; i < binFolderPaths.Length; i++)
            {
                if (Directory.Exists(Path.Combine(binFolderPaths[i], twainFolderName)))
                {
                    binFolderPath = binFolderPaths[i];
                    break;
                }
            }

            if (binFolderPath != null)
            {
                if (use32BitDevice)
                    // get path to the TWAIN device manager 2.x (32-bit) from installation of VintaSoft TWAIN .NET SDK
                    return Path.Combine(binFolderPath, "TWAINDSM32", "TWAINDSM.DLL");
                else
                    // get path to the TWAIN device manager 2.x (64-bit) from installation of VintaSoft TWAIN .NET SDK
                    return Path.Combine(binFolderPath, "TWAINDSM64", "TWAINDSM.DLL");
            }

            return null;
        }

        #endregion

    }
}
