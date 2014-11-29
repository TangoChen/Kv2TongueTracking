using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Microsoft.Kinect;
using Microsoft.Kinect.Face;
using System.ComponentModel;

namespace Kv2TongueTracking
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window//, INotifyPropertyChanged
    {

        float _tongueX;

        /// <summary>
        /// Value 0 to 1, represents X axis of tongue's tip in the mouth.
        /// The smaller it is, the closer it is to the left.
        /// 0.5 represents that it is in the middle.
        /// </summary>
        public float TongueX
        {
            get
            {
                return _tongueX;
            }
        }

        float _tongueY;

        /// <summary>
        /// Value 0 to 1, represents Y axis of tongue's tip in the mouth.
        /// The smaller it is, the closer it is to the top.
        /// 0.5 represents that it is in the middle.
        /// </summary>
        public float TongueY
        {
            get
            {
                return _tongueY;
            }
        }

        string _direction;
        public string Direction
        {
            get
            {
                return _direction;
            }
        }

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Coordinate mapper to map one type of point to another
        /// </summary>
        private CoordinateMapper coordinateMapper = null;

        /// <summary>
        /// Reader for body frames
        /// </summary>
        private BodyFrameReader bodyFrameReader = null;

        /// <summary>
        /// Face frame sources
        /// </summary>
        private FaceFrameSource faceFrameSource = null;

        /// <summary>
        /// Face frame readers
        /// </summary>
        private FaceFrameReader faceFrameReader = null;

        /// <summary>
        /// Storage for face frame results
        /// </summary>
        //private FaceFrameResult faceFrameResult = null;

        /// <summary>
        /// Array to store bodies
        /// </summary>
        private Body[] bodies = null;

        /// <summary>
        /// Get status of whether the tracked user's mouth is open
        /// </summary>
        private bool isMouthOpen = false;

        /// <summary>
        /// Position of mouth's left corner
        /// </summary>
        private PointF mouthCornerLeft = new PointF();
        /// <summary>
        /// Position of mouth's right corner
        /// </summary>
        private PointF mouthCornerRight = new PointF();
        /// <summary>
        /// The Y-axis of mouth's center
        /// </summary>
        private int mouthCenterY = 0;
        /// <summary>
        /// The X-axis of mouth's left, equals mouthCornerLeft.X
        /// </summary>
        private int mouthLeft = 0;
        /// <summary>
        /// The Y-axis of mouth's top
        /// </summary>
        private int mouthTop = 0;
        /// <summary>
        /// Mouth's width
        /// </summary>
        private int mouthWidth = 0;
        /// <summary>
        /// Mouth's height
        /// </summary>
        private int mouthHeight = 0;

        /// <summary>
        /// Time counting used to reduce the frame of updating tongue direction.
        /// So that it won't be too fast for you to see the result.
        /// </summary>
        private int showInfoTimeCount = 0;
        /// <summary>
        /// Frame number required to show new tongue direction.
        /// </summary>
        private const int REQUIRED_UPDATE_INFO_FRAME = 4;

        /// <summary>
        /// To make it not flash between mouth open and closed so often, we can add a counting before it's marked as mouth closed.
        /// </summary>
        private int mouthClosedTimeCount = 0;

        // Frame number required to mark mouth closed.
        private const int REQUIRED_MOUTH_CLOSED_FRAME = 5;

        /// <summary>
        /// This is the minimize depth value as the smaller the depth value is, the closer it is to the Kinect sensor.
        /// It's used to store the depth value of tongue's tip.
        /// </summary>
        ushort mouthClosestDepth = 10000;
        /// <summary>
        /// Storing the depth index of tongue's tip.
        /// </summary>
        int mouthClosestDetphIndex = -1;

        #region Depth
        /// <summary>
        /// Map depth range to byte range
        /// </summary>
        private const int MapDepthToByte = 8000 / 256;

        /// <summary>
        /// Reader for depth frames
        /// </summary>
        private DepthFrameReader depthFrameReader = null;

        /// <summary>
        /// Description of the data contained in the depth frame
        /// </summary>
        private FrameDescription depthFrameDescription = null;

        /// <summary>
        /// Bitmap to display
        /// </summary>
        private WriteableBitmap depthBitmap = null;

        /// <summary>
        /// Intermediate storage for frame data converted to color
        /// </summary>
        private byte[] depthPixels = null;

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource
        {
            get
            {
                //MessageBox.Show("..");
                return this.depthBitmap;
            }
        }
        #endregion





        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            // one sensor is currently supported
            this.kinectSensor = KinectSensor.GetDefault();

            this.bodies = new Body[this.kinectSensor.BodyFrameSource.BodyCount];

            // get the coordinate mapper
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;

            // get the color frame details
            FrameDescription frameDescription = this.kinectSensor.ColorFrameSource.FrameDescription;

            // specify the required face frame results
            FaceFrameFeatures faceFrameFeatures =
                FaceFrameFeatures.BoundingBoxInInfraredSpace
                | FaceFrameFeatures.PointsInInfraredSpace
                | FaceFrameFeatures.RotationOrientation
                | FaceFrameFeatures.MouthOpen;

            // create a face frame source + reader to track each face in the FOV
            this.faceFrameSource = new FaceFrameSource(this.kinectSensor, 0, faceFrameFeatures);
            this.faceFrameReader = this.faceFrameSource.OpenReader();
            //faceFrameResult = new FaceFrameResult();

            #region Depth
            // open the reader for the depth frames
            this.depthFrameReader = this.kinectSensor.DepthFrameSource.OpenReader();

            // wire handler for frame arrival
            this.depthFrameReader.FrameArrived += this.Reader_FrameArrived;

            // get FrameDescription from DepthFrameSource
            this.depthFrameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

            // allocate space to put the pixels being received and converted
            this.depthPixels = new byte[this.depthFrameDescription.Width * this.depthFrameDescription.Height];

            // create the bitmap to display
            this.depthBitmap = new WriteableBitmap(this.depthFrameDescription.Width, this.depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Gray8, null);
            #endregion

            // open the reader for the body frames
            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();

            // wire handler for body frame arrival
            this.bodyFrameReader.FrameArrived += this.Reader_BodyFrameArrived;

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // open the sensor
            this.kinectSensor.Open();

            // initialize the components (controls) of the window
            this.InitializeComponent();
        }

        /// <summary>
        /// Execute start up tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.faceFrameReader != null)
            {
                // wire handler for face frame arrival
                this.faceFrameReader.FrameArrived += this.Reader_FaceFrameArrived;
            }

            if (this.bodyFrameReader != null)
            {
                // wire handler for body frame arrival
                this.bodyFrameReader.FrameArrived += this.Reader_BodyFrameArrived;
            }

        }

        /// <summary>
        /// Handles the body frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_BodyFrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            using (var bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    // update body data
                    bodyFrame.GetAndRefreshBodyData(this.bodies);
                    //MessageBox.Show(".");
                    //this.Title = faceFrameSource.TrackingId.ToString();
                    // check if a valid face is tracked in this face source
                    if (!this.faceFrameSource.IsTrackingIdValid)
                    {
                        //this.Title = "Id Invalid";
                        foreach (Body body in bodies)
                        {
                            // check if the corresponding body is tracked 
                            if (body.IsTracked)
                            {
                                // update the face frame source to track this body
                                this.faceFrameSource.TrackingId = body.TrackingId;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handles the face frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_FaceFrameArrived(object sender, FaceFrameArrivedEventArgs e)
        {
            using (FaceFrame faceFrame = e.FrameReference.AcquireFrame())
            {
                if (faceFrame != null)
                {
                    if (faceFrameSource != faceFrame.FaceFrameSource) return;
                    // store this face frame result
                    FaceFrameResult faceFrameResult = faceFrame.FaceFrameResult;
                    if (faceFrameResult != null && faceFrameResult.FaceProperties != null)
                    {
                        isMouthOpen = (faceFrameResult.FaceProperties[FaceProperty.MouthOpen] == (DetectionResult.Yes | DetectionResult.Maybe));
                        //isMouthOpen = (faceFrameResult.FaceProperties[FaceProperty.MouthOpen] != DetectionResult.No);
                        mouthCornerLeft = faceFrameResult.FacePointsInInfraredSpace[FacePointType.MouthCornerLeft];
                        mouthCornerRight = faceFrameResult.FacePointsInInfraredSpace[FacePointType.MouthCornerRight];
                        mouthCenterY = (int)((mouthCornerLeft.Y + mouthCornerRight.Y) / 2f);
                        mouthLeft = (int)mouthCornerLeft.X;
                        mouthWidth = (int)(mouthCornerRight.X - mouthCornerLeft.X);
                        mouthHeight = mouthWidth / 2;
                        mouthTop = mouthCenterY - mouthHeight / 2;
                    }

                }
            }
        }


        private void Reader_FrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            bool depthFrameProcessed = false;

            using (DepthFrame depthFrame = e.FrameReference.AcquireFrame())
            {
                if (depthFrame != null)
                {
                    // the fastest way to process the body index data is to directly access 
                    // the underlying buffer
                    using (Microsoft.Kinect.KinectBuffer depthBuffer = depthFrame.LockImageBuffer())
                    {
                        // verify data and write the color data to the display bitmap
                        if (((this.depthFrameDescription.Width * this.depthFrameDescription.Height) == (depthBuffer.Size / this.depthFrameDescription.BytesPerPixel)) &&
                            (this.depthFrameDescription.Width == this.depthBitmap.PixelWidth) && (this.depthFrameDescription.Height == this.depthBitmap.PixelHeight))
                        {
                            // Note: In order to see the full range of depth (including the less reliable far field depth)
                            // we are setting maxDepth to the extreme potential depth threshold
                            ushort maxDepth = ushort.MaxValue;

                            // If you wish to filter by reliable depth distance, uncomment the following line:
                            //// maxDepth = depthFrame.DepthMaxReliableDistance

                            this.ProcessDepthFrameData(depthBuffer.UnderlyingBuffer, depthBuffer.Size, depthFrame.DepthMinReliableDistance, maxDepth);
                            depthFrameProcessed = true;
                        }
                    }
                }
            }

            if (depthFrameProcessed)
            {
                this.RenderDepthPixels();
                //MessageBox.Show("...");
            }
        }

        /// <summary>
        /// Directly accesses the underlying image buffer of the DepthFrame to 
        /// create a displayable bitmap.
        /// This function requires the /unsafe compiler option as we make use of direct
        /// access to the native memory pointed to by the depthFrameData pointer.
        /// </summary>
        /// <param name="depthFrameData">Pointer to the DepthFrame image data</param>
        /// <param name="depthFrameDataSize">Size of the DepthFrame image data</param>
        /// <param name="minDepth">The minimum reliable depth value for the frame</param>
        /// <param name="maxDepth">The maximum reliable depth value for the frame</param>
        private unsafe void ProcessDepthFrameData(IntPtr depthFrameData, uint depthFrameDataSize, ushort minDepth, ushort maxDepth)
        {
            // depth frame data is a 16 bit value
            ushort* frameData = (ushort*)depthFrameData;

            mouthClosestDepth = 10000;
            mouthClosestDetphIndex = -1;

            // convert depth to a visual representation
            for (int depthIndex = 0; depthIndex < (int)(depthFrameDataSize / this.depthFrameDescription.BytesPerPixel); ++depthIndex)
            {
                // Get the depth for this pixel
                ushort depth = frameData[depthIndex];
                //if (inOnePoint(i, (int)mouthCornerLeft.X, (int)mouthCornerLeft.Y))
                if (inArea(depthIndex, mouthLeft, mouthTop, mouthWidth, mouthHeight))
                {
                    // Will be drawn as a white pixel
                    this.depthPixels[depthIndex] = (byte)255;
                    if (depth < mouthClosestDepth)
                    {
                        mouthClosestDepth = depth;
                        mouthClosestDetphIndex = depthIndex;
                    }
                }
                else
                {
                    // To convert to a byte, we're mapping the depth value to the byte range.
                    // Values outside the reliable depth range are mapped to 0 (black).
                    this.depthPixels[depthIndex] = (byte)(depth >= minDepth && depth <= maxDepth ? (depth / MapDepthToByte) : 0);
                }
            }

            if (mouthClosestDetphIndex != -1)
            {

                if (isMouthOpen)
                {
                    mouthClosedTimeCount = 0;

                    // Will be drawn as a black pixel
                    this.depthPixels[mouthClosestDetphIndex] = (byte)0;

                    if (++showInfoTimeCount <= REQUIRED_UPDATE_INFO_FRAME)
                    {
                        return;
                    }

                    showInfoTimeCount = 0;

                    _tongueX = (getX(mouthClosestDetphIndex) - mouthLeft) / (float)mouthWidth;
                    _tongueY = (getY(mouthClosestDetphIndex) - mouthTop) / (float)mouthHeight;

                    this.Title = _tongueX.ToString("f2") + ", " + _tongueY.ToString("f2");

                    if (_tongueX < 0.3)
                    {
                        if (_tongueY < 0.3)
                        {
                            _direction = "↖";
                        }
                        else if (_tongueY < 0.6)
                        {
                            _direction = "←";
                        }
                        else
                        {
                            _direction = "↙";
                        }
                    }
                    else if (_tongueX < 0.6)
                    {
                        if (_tongueY < 0.3)
                        {
                            _direction = "↑";
                        }
                        else if (_tongueY < 0.6)
                        {
                            _direction = "o";
                        }
                        else
                        {
                            _direction = "↓";
                        }
                    }
                    else
                    {
                        if (_tongueY < 0.3)
                        {
                            _direction = "↗";
                        }
                        else if (_tongueY < 0.6)
                        {
                            _direction = "→";
                        }
                        else
                        {
                            _direction = "↘";
                        }
                    }
                }
                #region Uncomment this region if you want it to show an "X" when mouth is not open.
                else if (++mouthClosedTimeCount >= REQUIRED_MOUTH_CLOSED_FRAME)
                {
                    _direction = "X";
                }
                #endregion

                tbDirection.Text = _direction;

            }

        }

        /// <summary>
        /// Renders color pixels into the writeableBitmap.
        /// </summary>
        private void RenderDepthPixels()
        {
            this.depthBitmap.WritePixels(
                new Int32Rect(0, 0, this.depthBitmap.PixelWidth, this.depthBitmap.PixelHeight),
                this.depthPixels,
                this.depthBitmap.PixelWidth,
                0);

            image.Source = depthBitmap;
        }


        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            if (this.kinectSensor != null)
            {
                // on failure, set the status text
                this.Title = "Kv2 Tongue Detector - " + (this.kinectSensor.IsAvailable ? "Running" : "Sensor Not Available");
            }
        }


        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.depthFrameReader != null)
            {
                // DepthFrameReader is IDisposable
                this.depthFrameReader.Dispose();
                this.depthFrameReader = null;
            }

            if (this.bodyFrameReader != null)
            {
                // BodyFrameReader is IDisposable
                this.bodyFrameReader.Dispose();
                this.bodyFrameReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        /// <summary>
        /// Check if the pixel is the in the position (x, y).
        /// </summary>
        /// <param name="depthIndex"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public bool inOnePoint(int depthIndex, int x, int y)
        {
            int depthY = (depthIndex + 1) / depthFrameDescription.Width;
            int depthX = (depthIndex + 1) % depthFrameDescription.Width;
            if (depthX == x && depthY == y)
            {
                return true;
            }
            else
            {
                return false;
            }

        }

        /// <summary>
        /// Check if the pixel is in the rectangle area.
        /// </summary>
        /// <param name="depthIndex"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public bool inArea(int depthIndex, int x, int y, int width, int height)
        {
            int depthY = (depthIndex + 1) / depthFrameDescription.Width;
            int depthX = (depthIndex + 1) % depthFrameDescription.Width;
            if (depthY > y && depthY < y + height && depthX > x && depthX < x + width)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Get the X-axis of the pixel's position.
        /// </summary>
        /// <param name="depthIndex"></param>
        /// <returns></returns>
        public int getX(int depthIndex)
        {
            return (depthIndex + 1) % depthFrameDescription.Width;
        }

        /// <summary>
        /// Get the Y-axis of the pixel's position.
        /// </summary>
        /// <param name="depthIndex"></param>
        /// <returns></returns>
        public int getY(int depthIndex)
        {
            return (depthIndex + 1) / depthFrameDescription.Width;
        }

    }
}
