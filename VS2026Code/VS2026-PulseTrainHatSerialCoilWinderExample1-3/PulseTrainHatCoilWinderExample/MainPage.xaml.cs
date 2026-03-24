using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

// Test program for Pulse Train Hat http://www.pthat.com

namespace PulseTrainHatSerialCoilWinderExample
{
    public sealed partial class MainPage : Page
    {
        /// <summary>
        /// Private variables
        /// </summary>
        private SerialDevice serialPort = null;

        private DataWriter dataWriteObject = null;
        private DataReader dataReaderObject = null;

        private ObservableCollection<DeviceInformation> listOfDevices;
        private CancellationTokenSource ReadCancellationTokenSource;

        //Axis set flag
        private int Xset = 0;
        private int Yset = 0;
        private int Runset = 0;

        //Axis complete flag
        private int Xsetrun = 0;
        private int Ysetrun = 0;

        //Direction flag
        private int XdirectionChange = 0;

        //Auto count flag
        private int XAutoCountFeedback = 0;
        private int XPulseCountback = 0;
        private int YPulseCountback = 0;

        //Axis is running flag
        private int Running = 0;

        //Pause flag
        private int pausetriggered = 0;
        private int pauseAllaxis = 0;

        //Stop has been pressed flag
        private int stoptriggered = 0;

        //Temporary calculations
        private dynamic tempcalc1;
        private dynamic tempcalc2;

        //Conversion Variables
        private double convertRPM;
        private double convertSTEPS;

        public MainPage()
        {
            this.InitializeComponent();

            PauseAll.Background = new SolidColorBrush(Windows.UI.Colors.LightBlue);
            comPortInput.IsEnabled = false;
            Firmware1.IsEnabled = false;
            StartAll.IsEnabled = false;
            PauseAll.IsEnabled = false;
            StopAll.IsEnabled = false;
            Reset.IsEnabled = false;
            ToggleEnableLine.IsEnabled = false;
            listOfDevices = new ObservableCollection<DeviceInformation>();
            ListAvailablePorts();
            formatboxes();
        }

        /// <summary>
        /// ListAvailablePorts
        /// - Use SerialDevice.GetDeviceSelector to enumerate all serial devices
        /// - Attaches the DeviceInformation to the ListBox source so that DeviceIds are displayed
        /// </summary>
        private async void ListAvailablePorts()
        {
            try
            {
                string aqs = SerialDevice.GetDeviceSelector();
                var dis = await DeviceInformation.FindAllAsync(aqs);

                status.Text = "Select a device and connect";

                for (int i = 0; i < dis.Count; i++)
                {
                    listOfDevices.Add(dis[i]);
                }

                DeviceListSource.Source = listOfDevices;
                comPortInput.IsEnabled = true;
                ConnectDevices.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
            }
        }

        /// <summary>
        /// comPortInput_Click: Action to take when 'Connect' button is clicked
        /// - Get the selected device index and use Id to create the SerialDevice object
        /// - Configure default settings for the serial port
        /// - Create the ReadCancellationTokenSource token
        /// - Start listening on the serial port input
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void comPortInput_Click(object sender, RoutedEventArgs e)
        {
            var selection = ConnectDevices.SelectedItems;

            if (selection.Count <= 0)
            {
                status.Text = "Select a device and connect";
                return;
            }

            DeviceInformation entry = (DeviceInformation)selection[0];

            try
            {
                serialPort = await SerialDevice.FromIdAsync(entry.Id);

                // Disable the 'Connect' button
                comPortInput.IsEnabled = false;

                // Configure serial settings
                serialPort.WriteTimeout = TimeSpan.FromMilliseconds(10);
                serialPort.ReadTimeout = TimeSpan.FromMilliseconds(35);
                if (LowSpeedBaud.IsChecked == true)
                {
                    serialPort.BaudRate = 115200;
                }
                else
                {
                    serialPort.BaudRate = 806400;
                }
                serialPort.Parity = SerialParity.None;
                serialPort.StopBits = SerialStopBitCount.One;
                serialPort.DataBits = 8;
                serialPort.Handshake = SerialHandshake.None;

                // Display configured settings
                status.Text = "Serial port configured successfully: ";
                status.Text += serialPort.BaudRate + "-";
                status.Text += serialPort.DataBits + "-";
                status.Text += serialPort.Parity.ToString() + "-";
                status.Text += serialPort.StopBits;

                // Set the RcvdText field to invoke the TextChanged callback
                // The callback launches an async Read task to wait for data
                rcvdText.Text = "Waiting for data...";

                // Create cancellation token object to close I/O operations when closing the device
                ReadCancellationTokenSource = new CancellationTokenSource();

                // Enable 'Start' button to allow sending data

                Firmware1.IsEnabled = true;
                Reset.IsEnabled = true;
                ToggleEnableLine.IsEnabled = true;
                sendText.Text = "";
                StartAll.IsEnabled = true;

                Listen();
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
                comPortInput.IsEnabled = true;

                Firmware1.IsEnabled = false;
                StartAll.IsEnabled = false;
                PauseAll.IsEnabled = false;
                StopAll.IsEnabled = false;
                Reset.IsEnabled = false;
                ToggleEnableLine.IsEnabled = false;
            }
        }

        /// <summary>
        /// WriteAsync: Task that asynchronously writes data from the input text box 'sendText' to the OutputStream
        /// </summary>
        /// <returns></returns>
        private async Task WriteAsync()
        {
            Task<UInt32> storeAsyncTask;

            if (Xset == 1) //Send Set X Command
            {
                // Load the text from the sendText input text box to the dataWriter object
                dataWriteObject.WriteString(sendText.Text);

                // Launch an async task to complete the write operation
                storeAsyncTask = dataWriteObject.StoreAsync().AsTask();

                UInt32 bytesWritten = await storeAsyncTask;
                if (bytesWritten > 0)
                {
                    status.Text = sendText.Text + ", ";
                    status.Text += "bytes written successfully!";
                }
            }

            if (Yset == 1) //Send Set Y Command
            {
                dataWriteObject.WriteString(sendText1.Text);

                storeAsyncTask = dataWriteObject.StoreAsync().AsTask();

                UInt32 bytesWritten = await storeAsyncTask;
                if (bytesWritten > 0)
                {
                    status.Text = sendText1.Text + ", ";
                    status.Text += "bytes written successfully!";
                }
            }

            if (XdirectionChange == 1) //Send Auto Change Direction Command
            {
                dataWriteObject.WriteString(sendText2.Text);

                storeAsyncTask = dataWriteObject.StoreAsync().AsTask();

                UInt32 bytesWritten = await storeAsyncTask;
                if (bytesWritten > 0)
                {
                    status.Text = sendText2.Text + ", ";
                    status.Text += "bytes written successfully!";
                }
            }

            if (XAutoCountFeedback == 1) //Send Auto Count Feedback
            {
                dataWriteObject.WriteString(sendText3.Text);

                storeAsyncTask = dataWriteObject.StoreAsync().AsTask();

                UInt32 bytesWritten = await storeAsyncTask;
                if (bytesWritten > 0)
                {
                    status.Text = sendText3.Text + ", ";
                    status.Text += "bytes written successfully!";
                }
            }

            if (Runset == 1)
            {
                dataWriteObject.WriteString(sendText4.Text);

                storeAsyncTask = dataWriteObject.StoreAsync().AsTask();

                UInt32 bytesWritten = await storeAsyncTask;
                if (bytesWritten > 0)
                {
                    status.Text = sendText4.Text + ", ";
                    status.Text += "bytes written successfully!";
                }
            }

            if (XPulseCountback == 1)
            {
                dataWriteObject.WriteString("I00XP*");

                storeAsyncTask = dataWriteObject.StoreAsync().AsTask();

                UInt32 bytesWritten = await storeAsyncTask;
                if (bytesWritten > 0)
                {
                    status.Text = "I00XP*" + ", ";
                    status.Text += "bytes written successfully!";
                }
            }

            if (YPulseCountback == 1)
            {
                dataWriteObject.WriteString("I00YP*");

                storeAsyncTask = dataWriteObject.StoreAsync().AsTask();

                UInt32 bytesWritten = await storeAsyncTask;
                if (bytesWritten > 0)
                {
                    status.Text = "I00YP*" + ", ";
                    status.Text += "bytes written successfully!";
                }
            }
        }

        /// <summary>
        /// - Create a DataReader object
        /// - Create an async task to read from the SerialDevice InputStream
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Listen()
        {
            try
            {
                if (serialPort != null)
                {
                    dataReaderObject = new DataReader(serialPort.InputStream);

                    // keep reading the serial input
                    while (true)
                    {
                        await ReadAsync(ReadCancellationTokenSource.Token);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.GetType().Name == "TaskCanceledException")
                {
                    status.Text = "Reading task was cancelled, closing device and cleaning up";
                    CloseDevice();
                }
                else
                {
                    status.Text = ex.Message;
                }
            }
            finally
            {
                // Cleanup once complete
                if (dataReaderObject != null)
                {
                    dataReaderObject.DetachStream();
                    dataReaderObject = null;
                }
            }
        }

        /// <summary>
        /// ReadAsync: Task that waits on data and reads asynchronously from the serial device InputStream
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task ReadAsync(CancellationToken cancellationToken)
        {
            Task<UInt32> loadAsyncTask;

            uint ReadBufferLength = 1024;

            // If task cancellation was requested, comply
            cancellationToken.ThrowIfCancellationRequested();

            // Set InputStreamOptions to complete the asynchronous read operation when one or more bytes is available
            dataReaderObject.InputStreamOptions = InputStreamOptions.Partial;

            // Create a task object to wait for data on the serialPort.InputStream
            loadAsyncTask = dataReaderObject.LoadAsync(ReadBufferLength).AsTask(cancellationToken);

            // Launch the task and wait
            UInt32 bytesRead = await loadAsyncTask;

            if (bytesRead > 0)
            {
                rcvdText.Text = dataReaderObject.ReadString(bytesRead);
                string input = rcvdText.Text;

                //Check if received message can be divided by 7 as our return messages are 7 bytes long
                if (input.Length % 7 == 0)
                {
                    //*********
                    for (int i = 0; i < input.Length; i += 7)
                    {
                        string sub = input.Substring(i, 7);

                        //Check if Start ALL command Received
                        if (sub == "RI00SA*")
                        {
                            //Enable/Disable certain controls
                            StartAll.IsEnabled = false;
                            StopAll.IsEnabled = true;
                            PauseAll.IsEnabled = true;
                            ToggleEnableLine.IsEnabled = false;
                            Firmware1.IsEnabled = false;
                        }

                        //Check if Pause ALL command Received
                        if (sub == "RI00PA*")
                        {
                            //Replace pause All button text
                            PauseAll.Content = "Resume";

                            //Change pause All button colour
                            PauseAll.Background = new SolidColorBrush(Windows.UI.Colors.PaleGreen);

                            //Pause All is active
                            pauseAllaxis = 1;
                        }

                        //Check if Resume All command Received
                        if (sub == "CI00PA*")
                        {
                            PauseAll.Content = "Pause";
                            PauseAll.Background = new SolidColorBrush(Windows.UI.Colors.LightBlue);
                            pauseAllaxis = 0;
                        }

                        //Check if Set X Axis completed
                        if (sub == "CI00CX*")
                        {
                            // Once X Axis is Set then we call the SetYaxis routine
                            Xsetrun = 1;
                            SetYaxis();
                        }

                        //Check if Set Y Axis completed
                        if (sub == "CI00CY*")
                        {
                            // Once Y Axis is Set then we call the Set X Auto Change Direction routine
                            SetXdirection();
                            Ysetrun = 1;
                        }

                        //Check if Set X Direction Auto Change set
                        if (sub == "CI00BX*")
                        {
                            // Once X Axis X Auto Change Direction is set we then call the Auto Count Feedback Routine
                            Autocount();
                        }

                        //Check if Set Y Auto Count Feedback
                        if (sub == "CI00JY*")
                        {
                            // Once Auto Count Feedback is set we then call the Run all routine
                            RunSet();
                            Running = 1;
                        }

                        //Check if Set Y Auto Count Feedback
                        if (sub == "CI00JX*")
                        {
                            // Once Auto Count Feedback is set we then call the Run all routine
                            RunSet();
                            Running = 1;
                        }

                        //Check Auto Count data comes back
                        if (sub == "DI00JY*")
                        {
                            //Store Auto Count data
                            YpulseCountBack.Text = rcvdText.Text.Substring(i + 24, 10);
                            XpulseCountBack.Text = rcvdText.Text.Substring(i + 10, 10);
                            DirectionFeedback.Text = rcvdText.Text.Substring(i + 9, 1);

                            //Call calculation method
                            Calcpulseback();

                            //Wait for a Pulse Count update before sending pause command so we do not get conflict
                            if (pausetriggered == 1)
                            {
                                if (pauseAllaxis == 0)
                                {
                                    SendPause();
                                }

                                //Wait for a Pulse Count update before sending Stop command so we do not get conflict
                                if (stoptriggered == 1)
                                {
                                    StopAllMotors();
                                }
                            }
                        }

                        //Check Auto Count data comes back
                        if (sub == "DI00JX*")
                        {
                            //Store Auto Count data
                            YpulseCountBack.Text = rcvdText.Text.Substring(i + 24, 10);
                            XpulseCountBack.Text = rcvdText.Text.Substring(i + 10, 10);
                            DirectionFeedback.Text = rcvdText.Text.Substring(i + 9, 1);

                            //Call calculation method
                            Calcpulseback();

                            //Wait for a Pulse Count update before sending pause command so we do not get conflict
                            if (pausetriggered == 1)
                            {
                                if (pauseAllaxis == 0)
                                {
                                    SendPause();
                                }
                            }

                            //Wait for a Pulse Count update before sending Stop command so we do not get conflict
                            if (stoptriggered == 1)
                            {
                                StopAllMotors();
                            }
                        }

                        //Check Pulse X Count Back
                        if (sub == "RI00XP*")
                        {
                            XpulseCountBack.Text = rcvdText.Text.Substring(i + 10, 10);
                            DirectionFeedback.Text = rcvdText.Text.Substring(i + 9, 1);
                            Calcpulseback();

                            //Call Method to set flags and send data to PTHAT
                            PulsesbackY();
                        }

                        //Check Pulse Y Count Back
                        if (sub == "RI00YP*")
                        {
                            YpulseCountBack.Text = rcvdText.Text.Substring(i + 10, 10);
                            Calcpulseback();
                        }

                        //Check Pulse X Count Back
                        if (sub == "DI00PX*")
                        {
                            XpulseCountBack.Text = rcvdText.Text.Substring(i + 10, 10);
                            DirectionFeedback.Text = rcvdText.Text.Substring(i + 9, 1);
                            Calcpulseback();
                        }
                        //Check Pulse Y Count Back
                        if (sub == "DI00PY*")
                        {
                            YpulseCountBack.Text = rcvdText.Text.Substring(i + 10, 10);
                            Calcpulseback();
                        }

                        //Check For Firmware reply Back
                        if (sub == "RI00FW*")
                        {
                            rcvdText.Text = rcvdText.Text.Substring(i + 8, 40);
                        }

                        //Check if ALL Axis Stop button Complete
                        if (sub == "CI00TA*")
                        {
                            //Enable/Disable certain controls
                            StopAll.IsEnabled = false;
                            PauseAll.IsEnabled = false;
                            StartAll.IsEnabled = true;
                            ToggleEnableLine.IsEnabled = true;
                            Firmware1.IsEnabled = true;

                            //Sets trigger variable
                            stoptriggered = 0;

                            //Checks if paused
                            if (pauseAllaxis == 1)
                            {
                                PauseAll.Content = "Pause";
                                PauseAll.Background = new SolidColorBrush(Windows.UI.Colors.LightBlue);
                                pauseAllaxis = 0;
                            }
                        }

                        //Check if X Axis completed amount of pulses
                        if (sub == "CI00SX*")

                        {
                            Xsetrun = 0;
                        }

                        //Check if Y Axis completed amount of pulses
                        if (sub == "CI00SY*")
                        {
                            Ysetrun = 0;
                        }

                        //Check if all completed
                        int checkall = Xsetrun + Ysetrun;
                        if (checkall == 0)
                        {
                            StopAll.IsEnabled = false;
                            PauseAll.IsEnabled = false;
                            ToggleEnableLine.IsEnabled = true;
                            Firmware1.IsEnabled = true;
                            StartAll.IsEnabled = true;

                            //Now get final X pulse count back
                            if (Running == 1)

                            {
                                PulsesbackX();
                                Running = 0;
                            }
                        }
                    } // end of for loop
                } //endof checking length if

                status.Text = "bytes read successfully!";
            } //End of checking for bytes
        } //end of async read

        private void Calcpulseback()
        {
            BobbinTurns.Text = Convert.ToString(Convert.ToDouble(YpulseCountBack.Text) / Convert.ToDouble(StepsPerRev.Text));
            tempcalc1 = Math.Floor(Convert.ToDouble(XpulseCountBack.Text) / Convert.ToDouble(CalculatedXDirectionChange.Text));
            tempcalc2 = tempcalc1 * Convert.ToDouble(CalculatedXDirectionChange.Text);
            tempcalc1 = Convert.ToDouble(XpulseCountBack.Text) - tempcalc2;

            if (DirectionFeedback.Text == Xdir.Text)
            {
                tempcalc2 = tempcalc1 * Convert.ToDouble(Stepsmm.Text);
                Feederposition.Text = Convert.ToString(tempcalc2);
            }
            else
            {
                tempcalc2 = Convert.ToDouble(CalculatedXDirectionChange.Text) - tempcalc1;
                tempcalc1 = tempcalc2 * Convert.ToDouble(Stepsmm.Text);
                Feederposition.Text = Convert.ToString(tempcalc1);
            }
            Feederposition.Text = String.Format("{0:00.0000}", Convert.ToDouble(Feederposition.Text));

            BobbinTurns.Text = String.Format("{0:000000}", Convert.ToDouble(BobbinTurns.Text));
        }

        /// <summary>
        /// CancelReadTask:
        /// - Uses the ReadCancellationTokenSource to cancel read operations
        /// </summary>
        private void CancelReadTask()
        {
            if (ReadCancellationTokenSource != null)
            {
                if (!ReadCancellationTokenSource.IsCancellationRequested)
                {
                    ReadCancellationTokenSource.Cancel();
                }
            }
        }

        /// <summary>
        /// CloseDevice:
        /// - Disposes SerialDevice object
        /// - Clears the enumerated device Id list
        /// </summary>
        private void CloseDevice()
        {
            if (serialPort != null)
            {
                serialPort.Dispose();
            }
            serialPort = null;
            comPortInput.IsEnabled = true;
            rcvdText.Text = "";
            listOfDevices.Clear();
        }

        /// <summary>
        /// closeDevice_Click: Action to take when 'Disconnect and Refresh List' is clicked on
        /// - Cancel all read operations
        /// - Close and dispose the SerialDevice object
        /// - Enumerate connected devices
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void closeDevice_Click(object sender, RoutedEventArgs e)
        {
            Disconnectserial();
        }

        private void Disconnectserial()
        {
            try
            {
                status.Text = "";
                CancelReadTask();
                CloseDevice();
                ListAvailablePorts();
                Firmware1.IsEnabled = false;
                StartAll.IsEnabled = false;
                PauseAll.IsEnabled = false;
                StopAll.IsEnabled = false;
                Reset.IsEnabled = false;
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
            }
        }

        //Requests the Firmware Version from the PTHAT
        private void Firmware_Click(object sender, RoutedEventArgs e)
        {
            SetFlags(ref Xset);
            sendText.Text = "I00FW*";
            SendDataout();
        }

        //Send Set X command
        private void SetXaxis()
        {
            SetFlags(ref Xset);
            SendDataout();
        }

        //Send Set Y command
        private void SetYaxis()
        {
            SetFlags(ref Yset);
            SendDataout();
        }

        //Send Set X Direction Auto Change
        private void SetXdirection()
        {
            SetFlags(ref XdirectionChange);
            SendDataout();
        }

        //Send Set Auto Count Feedback
        private void Autocount()
        {
            SetFlags(ref XAutoCountFeedback);
            SendDataout();
        }

        //Send Set Start All command
        private void RunSet()
        {
            SetFlags(ref Runset);
            SendDataout();
        }

        //Send Request Pulses Back from X
        private void PulsesbackX()
        {
            SetFlags(ref XPulseCountback);
            SendDataout();
        }

        //Send Request Pulses Back from Y
        private void PulsesbackY()
        {
            SetFlags(ref YPulseCountback);
            SendDataout();
        }

        private void StopAllMotors()
        {
            pausetriggered = 0;
            SetFlags(ref Xset);
            //Sends a Stop Command
            sendText.Text = "I00TA*";
            SendDataout();
        }

        private void SendPause()
        {
            SetFlags(ref Xset);

            //Send Pause All Command
            sendText.Text = "I00PA1100*";
            SendDataout();
        }

        private void ToggleEnableLine_Click(object sender, RoutedEventArgs e)
        {
            SetFlags(ref Xset);
            //Send Pause All Command
            sendText.Text = "I00HT*";
            SendDataout();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            SetFlags(ref Xset);
            //Send Reset Command
            sendText.Text = "N*";
            SendDataout();
            Disconnectserial();
        }

        private void SetFlags(ref int i)
        {
            Xset = 0;
            Yset = 0;
            XdirectionChange = 0;
            XAutoCountFeedback = 0;
            Runset = 0;
            XPulseCountback = 0;
            YPulseCountback = 0;

            i = 1;
        }

        private async void SendDataout()
        {
            try
            {
                if (serialPort != null)
                {
                    // Create the DataWriter object and attach to OutputStream
                    dataWriteObject = new DataWriter(serialPort.OutputStream);

                    //Launch the WriteAsync task to perform the write
                    await WriteAsync();
                }
                else
                {
                    status.Text = "Select a device and connect";
                }
            }
            catch (Exception ex)
            {
                status.Text = "Send Data: " + ex.Message;
            }
            finally
            {
                // Cleanup once complete
                if (dataWriteObject != null)
                {
                    dataWriteObject.DetachStream();
                    dataWriteObject = null;
                }
            }
        }

        private void formatboxes()
        {
            if (Convert.ToDouble(Stepsmm.Text) > 0)
            {
                //Calculate Wire Size to suit resolution
                tempcalc1 = Convert.ToDouble(WireSize.Text) / Convert.ToDouble(Stepsmm.Text);
                int tempwirecalc = Convert.ToInt16(tempcalc1);
                tempcalc2 = tempwirecalc * Convert.ToDouble(Stepsmm.Text);
                CalculatedWireSize.Text = Convert.ToString(tempcalc2);
                CalculatedWireSize.Text = String.Format("{0:0.0000}", Convert.ToDouble(CalculatedWireSize.Text));
            }

            if (Convert.ToDouble(WireSize.Text) > 0)
            {
                //Calculate Bobbin Width needed based on wire result
                tempcalc1 = Convert.ToDouble(BobbinWidth.Text) / Convert.ToDouble(CalculatedWireSize.Text);
                int tempbobbincalc = Convert.ToInt16(tempcalc1);
                tempcalc2 = tempbobbincalc * Convert.ToDouble(CalculatedWireSize.Text);
                CalculatedBobbinWidth.Text = Convert.ToString(tempcalc2);
                CalculatedBobbinWidth.Text = String.Format("{0:0.0000}", Convert.ToDouble(CalculatedBobbinWidth.Text));
            }

            //Calculate Turns Per Layer
            tempcalc1 = Convert.ToDouble(CalculatedBobbinWidth.Text) / Convert.ToDouble(CalculatedWireSize.Text);
            CalculatedTPL.Text = Convert.ToString(tempcalc1);
            CalculatedTPL.Text = String.Format("{0:0000000000}", Convert.ToDouble(CalculatedTPL.Text));

            //Calculate Total Layers
            tempcalc1 = Convert.ToDouble(TotalWindings.Text) / Convert.ToDouble(CalculatedTPL.Text);
            CalculatedLayers.Text = Convert.ToString(tempcalc1);
            CalculatedLayers.Text = String.Format("{0:000000.00}", Convert.ToDouble(CalculatedLayers.Text));

            //Calculate Pulses per layer for X feeder change direction
            tempcalc1 = Convert.ToDouble(CalculatedWireSize.Text) * Convert.ToDouble(CalculatedTPL.Text);
            tempcalc2 = tempcalc1 / Convert.ToDouble(Stepsmm.Text);
            CalculatedXDirectionChange.Text = Convert.ToString(tempcalc2);
            CalculatedXDirectionChange.Text = String.Format("{0:0000000000}", Convert.ToDouble(CalculatedXDirectionChange.Text));

            //Calculate Total pulses for Bobbin Motor Y
            tempcalc1 = Convert.ToDouble(TotalWindings.Text) * Convert.ToDouble(StepsPerRev.Text);
            Ypulsecount.Text = Convert.ToString(tempcalc1);

            //Calculate Total pulses for Feeder X
            tempcalc1 = Convert.ToDouble(CalculatedWireSize.Text) / Convert.ToDouble(Stepsmm.Text);
            tempcalc2 = tempcalc1 * Convert.ToDouble(TotalWindings.Text);
            Xpulsecount.Text = Convert.ToString(tempcalc2);

            HZresult.Text = String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text));

            //Calculate the Linear Interpolation so both motors stop and start at the same time

            //Check which motor has more pulses to go
            double xtargetCalc = Convert.ToDouble(Xpulsecount.Text);
            double ytargetCalc = Convert.ToDouble(Ypulsecount.Text);

            if (xtargetCalc == ytargetCalc) //Is X and Y Pulses Equal
            {
                XFreq.Text = HZresult.Text;
                YFreq.Text = HZresult.Text;
            }
            else //Not Equal so now see which is more pulses
            {
                if (xtargetCalc > ytargetCalc) //Is X more pulses than Y
                {
                    XFreq.Text = HZresult.Text;
                    tempcalc1 = xtargetCalc / ytargetCalc;
                    tempcalc2 = Convert.ToDouble(HZresult.Text) / tempcalc1;
                    //Convert our value to match the DDS Resolution
                    tempcalc2 = (Math.Round(tempcalc2 / 0.004)) * 0.004;
                    YFreq.Text = Convert.ToString(tempcalc2);
                }
                else //Y must be more pulses
                {
                    YFreq.Text = HZresult.Text;
                    tempcalc1 = ytargetCalc / xtargetCalc;
                    tempcalc2 = Convert.ToDouble(HZresult.Text) / tempcalc1;
                    tempcalc2 = (Math.Round(tempcalc2 / 0.004)) * 0.004;
                    XFreq.Text = Convert.ToString(tempcalc2);
                }
            }

            //Change direction
            Ydir.Text = (BobbinMotorCW.IsChecked == true) ? "0" : "1";
            Xdir.Text = (FeederCW.IsChecked == true) ? "0" : "1";

            //Format X Command
            XFreq.Text = String.Format("{0:000000.000}", Convert.ToDouble(XFreq.Text));
            Xpulsecount.Text = String.Format("{0:0000000000}", Convert.ToDouble(Xpulsecount.Text));
            Xdir.Text = String.Format("{0:0}", Convert.ToDouble(Xdir.Text));
            rampdivide.Text = String.Format("{0:000}", Convert.ToDouble(rampdivide.Text));
            ramppause.Text = String.Format("{0:000}", Convert.ToDouble(ramppause.Text));
            EnablePolarity.Text = String.Format("{0:0}", Convert.ToDouble(EnablePolarity.Text));

            //Set the X Set command format
            sendText.Text = "I00CX" + XFreq.Text + Xpulsecount.Text + Xdir.Text + "1" + "1" + rampdivide.Text + ramppause.Text + "0" + EnablePolarity.Text + "*";

            //Format Y Command
            YFreq.Text = String.Format("{0:000000.000}", Convert.ToDouble(YFreq.Text));
            Ypulsecount.Text = String.Format("{0:0000000000}", Convert.ToDouble(Ypulsecount.Text));
            Ydir.Text = String.Format("{0:0}", Convert.ToDouble(Ydir.Text));

            //Set the Y Set command format
            sendText1.Text = "I00CY" + YFreq.Text + Ypulsecount.Text + Ydir.Text + "1" + "1" + rampdivide.Text + ramppause.Text + "0" + EnablePolarity.Text + "*";

            //Set the pulses for direction change on X feeder arm
            sendText2.Text = "I00BX" + CalculatedXDirectionChange.Text + "*";

            //Set final command to Start both motors
            sendText4.Text = "I00SA*";
            tempcalc1 = Convert.ToDouble(CalculatedWireSize.Text) / Convert.ToDouble(Stepsmm.Text);
            Pitchcalc.Text = Convert.ToString(tempcalc1);
            Pitchcalc.Text = String.Format("{0:0000000000}", Convert.ToDouble(Pitchcalc.Text));
            sendText3.Text = "I00JX" + Pitchcalc.Text + "1100*";
            StepsPerRev.Text = String.Format("{0:0000000000}", Convert.ToDouble(StepsPerRev.Text));
        }

        private void StartAll_Click(object sender, RoutedEventArgs e)
        {
            stoptriggered = 0;
            pausetriggered = 0;
            Xdir.Text = "0";
            XpulseCountBack.Text = "";
            YpulseCountBack.Text = "";
            Feederposition.Text = "00.0000";
            BobbinTurns.Text = "000000";
            formatboxes();
            SetXaxis();
        }

        private void StopAll_Click(object sender, RoutedEventArgs e)
        {
            StopAllMotors();
        }

        private void PauseAll_Click(object sender, RoutedEventArgs e)
        {
            if (pausetriggered == 0)
            {
                pausetriggered = 1;
            }
            else
            {
                pausetriggered = 0;
                SendPause();
            }
        }

        private void RPM_TextChanged(object sender, TextChangedEventArgs e)
        {
            Conversions();
        }

        private void StepsPerRev_TextChanged(object sender, TextChangedEventArgs e)
        {
            Conversions();
        }

        private void Conversions()
        {
            //Check if Steps per revolution Textbox is not Null or empty
            if (!String.IsNullOrEmpty(StepsPerRev.Text.Trim()))
            {
                //Convert our Steps per revolution into a Frequency
                convertSTEPS = Convert.ToDouble(StepsPerRev.Text) * 0.0166666666666667;
            }

            //Check if RPM Textbox is not Null or empty
            if (!String.IsNullOrEmpty(RPM.Text.Trim()))
            {
                //Multiply the RPM by our new frequency
                convertRPM = Convert.ToDouble(RPM.Text) * convertSTEPS;

                //Convert our value to match the DDS Resolution
                convertRPM = (Math.Round(convertRPM / 0.004)) * 0.004;
            }

            //Format string
            HZresult.Text = Convert.ToString(convertRPM);
            HZresult.Text = String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text));
        }

        private void StepsPerRevFeeder_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!String.IsNullOrEmpty(StepsPerRevFeeder.Text.Trim()))
            {
                //Calculate Resolution of Feeder If Pulses per rev change
                Stepsmm.Text = String.Format("{0:0.0000}", Convert.ToDouble(BallscrewPitch.Text) / Convert.ToDouble(StepsPerRevFeeder.Text));
            }
        }

        private void BallscrewPitch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!String.IsNullOrEmpty(StepsPerRevFeeder.Text.Trim()))
            {
                //Calculate Resolution of Feeder If Pulses per rev change
                Stepsmm.Text = String.Format("{0:0.0000}", Convert.ToDouble(BallscrewPitch.Text) / Convert.ToDouble(StepsPerRevFeeder.Text));

                //Call format method
                formatboxes();
            }
        }

        private void WireSize_TextChanged(object sender, TextChangedEventArgs e)
        {
            //Calls update method with Wiresize Textbox as the input
            UpdateTextChange(WireSize);
        }

        private void BobbinWidth_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateTextChange(BobbinWidth);
        }

        private void TotalWindings_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateTextChange(TotalWindings);
        }

        private void UpdateTextChange(TextBox input)
        {
            if (!String.IsNullOrEmpty(input.Text.Trim()))
            {
                formatboxes();
            }
        }

        private void formatboxes(object sender, RoutedEventArgs e)
        {
            formatboxes();
        }
    }
}