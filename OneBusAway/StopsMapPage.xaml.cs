/* Copyright 2013 Shawn Henry, Rob Smith, and Michael Friedman
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Phone.Controls;
using OneBusAway.WP7.ViewModel;
using System.Device.Location;
using OneBusAway.WP7.ViewModel.BusServiceDataStructures;
using Microsoft.Phone.Maps.Controls;
using System.Windows.Data;
using System.Collections;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Maps.Toolkit;
using System.Collections.ObjectModel;

namespace OneBusAway.WP7.View
{
    /// <summary>
    /// A full screen map of stops.
    /// </summary>
    /// <remarks>
    /// Supports user interaction.  Will reload stops when moved.  Touch a stop to bring up its detail page.
    /// </remarks>
    public partial class StopsMapPage : AViewPage
    {
        private StopsMapVM viewModel;
        private bool mapHasMoved;

        internal static double minZoomLevel = 15.5; //below this level we don't even bother querying

        public StopsMapPage()
            : base()
        {
            InitializeComponent();
            base.Initialize();

            viewModel = aViewModel as StopsMapVM;
            mapHasMoved = false;
            this.Loaded += new RoutedEventHandler(FullScreenMapPage_Loaded);

            this.DetailsMap.CenterChanged += new EventHandler<MapCenterChangedEventArgs>(DetailsMap_TargetViewChanged);
            this.DetailsMap.ZoomLevelChanged += DetailsMap_ZoomLevelChanged;

            SupportedOrientations = SupportedPageOrientation.Portrait;

#if SCREENSHOT
            SystemTray.IsVisible = false;
#endif

#if DEBUG
            if (Microsoft.Devices.Environment.DeviceType == Microsoft.Devices.DeviceType.Emulator)
            {
                Button zoomOutBtn = new Button();
                zoomOutBtn.Content = "Zoom Out";
                zoomOutBtn.Background = new SolidColorBrush(Colors.Transparent);
                zoomOutBtn.Foreground = new SolidColorBrush(Colors.Black);
                zoomOutBtn.BorderBrush = new SolidColorBrush(Colors.Black);
                zoomOutBtn.VerticalAlignment = System.Windows.VerticalAlignment.Bottom;
                zoomOutBtn.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                zoomOutBtn.Click += new RoutedEventHandler(zoomOutBtn_Click);
                zoomOutBtn.SetValue(Canvas.ZIndexProperty, 30);
                zoomOutBtn.SetValue(Grid.RowProperty, 2);
                LayoutRoot.Children.Add(zoomOutBtn);
            }
#endif
        }

#if DEBUG
        void zoomOutBtn_Click(object sender, RoutedEventArgs e)
        {
            DetailsMap.ZoomLevel--;
        }
#endif

        void FullScreenMapPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (viewModel.CurrentViewState.CurrentSearchLocation != null)
            {
                // Using mapHasMoved prevents us from relocating the map if the user reloads this
                // page from the back stack
                if (mapHasMoved == false)
                {
                    Dispatcher.BeginInvoke(() =>
                        {
                            //DetailsMap.Center = viewModel.CurrentViewState.CurrentSearchLocation.location;
                            DetailsMap.SetView(viewModel.CurrentViewState.CurrentSearchLocation.boundingBox);
                            viewModel.LoadStopsForLocation(viewModel.CurrentViewState.CurrentSearchLocation.location);
                        }
                    );
                }
            }
            else
            {
                viewModel.LocationTracker.RunWhenLocationKnown(delegate(GeoCoordinate location)
                    {
                        Dispatcher.BeginInvoke(() =>
                            {
                                // If the user has already moved the map, don't relocate it
                                if (mapHasMoved == false)
                                {
                                    DetailsMap.Center = location;
                                }
                                    
                                viewModel.LoadStopsForLocation(location);
                            }
                            );
                    }
                );
            }

            // UI elements that are children of maps apparently don't get created until the page loads
            // so these lines in the autogenerated code will always find nothing.
            // need to repeat them here
            this.StopInfoBox = ((Microsoft.Phone.Maps.Toolkit.Pushpin)(this.FindName("StopInfoBox")));
            this.PopupBtn = ((System.Windows.Controls.Button)(this.FindName("PopupBtn")));
            this.StopName = ((System.Windows.Controls.TextBlock)(this.FindName("StopName")));
            this.StopRoutes = ((System.Windows.Controls.TextBlock)(this.FindName("StopRoutes")));
            this.StopDirection = ((System.Windows.Controls.TextBlock)(this.FindName("StopDirection")));
        }

        void DetailsMap_TargetViewChanged(object sender, MapCenterChangedEventArgs e)
        {
            GeoCoordinate center = DetailsMap.Center;
            mapHasMoved = true;

            if (DetailsMap.ZoomLevel >= minZoomLevel)
            {
                viewModel.LoadStopsForLocation(center);
            }
        }

        void DetailsMap_ZoomLevelChanged(object sender, MapZoomLevelChangedEventArgs e)
        {
            // hide the stop popup if we've zoomed out too far.
            if (DetailsMap.ZoomLevel < minZoomLevel && StopInfoBox != null)
            {
                StopInfoBox.Visibility = Visibility.Collapsed;
            }
        }

        private bool LocationRectContainedBy(LocationRectangle outer, LocationRectangle inner)
        {
            // TODO: This algorithm will almost certainly fail around the equator
            if (Math.Abs(inner.North) < Math.Abs(outer.North) && 
                Math.Abs(inner.South) > Math.Abs(outer.South) &&
                Math.Abs(inner.West) < Math.Abs(outer.West) && 
                Math.Abs(inner.East) > Math.Abs(outer.East))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        protected override void OnNavigatedTo(System.Windows.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            viewModel.RegisterEventHandlers(Dispatcher);
        }

        protected override void OnNavigatedFrom(System.Windows.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            viewModel.UnregisterEventHandlers();
        }

        private void BusStopPushpin_Click(object sender, RoutedEventArgs e)
        {
            string selectedStopId = ((Button)sender).Tag as string;

            foreach (Stop stop in viewModel.StopsForLocation)
            {
                if (stop != null && stop.id == selectedStopId)
                {
                    if (selectedStopId.Equals(StopInfoBox.Tag))
                    {
                        // This is the currently selected stop, hide the popup
                        StopInfoBox.Visibility = Visibility.Collapsed;
                        StopInfoBox.Tag = null;
                    }
                    else
                    {
                        // open the popup with details about the stop
                        StopName.Text = stop.name;
                        StopRoutes.Text = (string)new StopRoutesConverter().Convert(stop, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture);
                        StopDirection.Text = (string)new StopDirectionConverter().Convert(stop, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture);
                        StopInfoBox.Visibility = Visibility.Visible;
                        StopInfoBox.GeoCoordinate = stop.location;
                        StopInfoBox.Tag = stop.id;
                    }     
               
                    break;
                }
            }
        }

        private void NavigateToDetailsPage(Stop stop)
        {
            viewModel.CurrentViewState.CurrentStop = stop;
            viewModel.CurrentViewState.CurrentRoute = null;
            viewModel.CurrentViewState.CurrentRouteDirection = null;

            NavigationService.Navigate(new Uri("/DetailsPage.xaml", UriKind.Relative));
        }

        private void PopupBtn_Click(object sender, RoutedEventArgs e)
        {
            string selectedStopId = StopInfoBox.Tag as string;

            foreach (Stop stop in viewModel.StopsForLocation)
            {
                if (stop != null && stop.id == selectedStopId)
                {
                    // Hide the pop-up for when they return
                    StopInfoBox.Visibility = Visibility.Collapsed;
                    StopInfoBox.Tag = null;

                    NavigateToDetailsPage(stop);

                    break;
                }
            }
        }
    }

    public class MaxZoomConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            double zoom = (double)value;
            bool visibility = (zoom < StopsMapPage.minZoomLevel);

            if (parameter != null && bool.Parse(parameter.ToString()) == false)
            {
                visibility = !visibility;
            }

            return visibility ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    
}
