﻿/* Copyright 2013 Shawn Henry, Rob Smith, and Michael Friedman
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
using System.Device.Location;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using Microsoft.Phone.Maps.Controls;
using Microsoft.Phone.Shell;
using OneBusAway.WP7.ViewModel;
using OneBusAway.WP7.ViewModel.AppDataDataStructures;
using OneBusAway.WP7.ViewModel.BusServiceDataStructures;
using Microsoft.Phone.Maps.Toolkit;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace OneBusAway.WP7.View
{
    public partial class DetailsPage : AViewPage
    {
        private RouteDetailsVM viewModel;

        private Uri unfilterRoutesIcon = new Uri("/Images/appbar.add.rest.png", UriKind.Relative);
        private Uri filterRoutesIcon = new Uri("/Images/appbar.minus.rest.png", UriKind.Relative);
        private Uri addFavoriteIcon = new Uri("/Images/appbar.favs.addto.rest.png", UriKind.Relative);
        private Uri deleteFavoriteIcon = new Uri("/Images/appbar.favs.del.rest.png", UriKind.Relative);

        private string unfilterRoutesText = "all routes";
        private string filterRoutesText = "filter routes";
        private string addFavoriteText = "add";
        private string deleteFavoriteText = "delete";

        private bool isFavorite;
        private bool isFiltered;
        private Popup popup;

        private const double minimumZoomRadius = 100 * 0.009 * 0.001; // 100 meters in degrees
        private const double maximumZoomRadius = 250 * 0.009; // 250 km in degrees
 
        private string isFilteredStateId
        {
            get
            {
                string s = Guid.NewGuid().ToString();
                if (viewModel != null && viewModel.CurrentViewState != null && viewModel.CurrentViewState.CurrentStop != null)
                {
                    s = string.Format("DetailsPage-IsFiltered-{0}", viewModel.CurrentViewState.CurrentStop.id);
                    if (viewModel.CurrentViewState.CurrentRouteDirection != null && viewModel.CurrentViewState.CurrentRoute != null)
                    {
                        s += string.Format("-{0}-{1}", viewModel.CurrentViewState.CurrentRoute.id, viewModel.CurrentViewState.CurrentRouteDirection.name);
                    }
                }

                return s;
            }
        }

        private ApplicationBarIconButton appbar_allroutes;

        private DispatcherTimer busArrivalUpdateTimer;

        public DetailsPage()
            : base()
        {
            InitializeComponent();
            base.Initialize();

            this.Loaded += new RoutedEventHandler(DetailsPage_Loaded);
            this.Unloaded += new RoutedEventHandler(DetailsPage_Unloaded);
            this.BackKeyPress += new EventHandler<System.ComponentModel.CancelEventArgs>(DetailsPage_BackKeyPress);

            appbar_favorite = ((ApplicationBarIconButton)ApplicationBar.Buttons[0]);

            viewModel = Resources["ViewModel"] as RouteDetailsVM;

            busArrivalUpdateTimer = new DispatcherTimer();
            busArrivalUpdateTimer.Interval = new TimeSpan(0, 0, 0, 30, 0); // 30 secs 
            busArrivalUpdateTimer.Tick += new EventHandler(busArrivalUpdateTimer_Tick);

            this.ApplicationBar.ForegroundColor = ((SolidColorBrush)Application.Current.Resources["OBAForegroundBrush"]).Color;
            // the native theme uses a shade of "gray" that is actually white or black with an alpha mask.
            // the appbar needs to be opaque.
            ColorAlphaConverter alphaConverter = new ColorAlphaConverter();
            SolidColorBrush appBarBrush = (SolidColorBrush)alphaConverter.Convert(
                                                            Application.Current.Resources["OBADarkBrush"],
                                                            typeof(SolidColorBrush),
                                                            Application.Current.Resources["OBABackgroundBrush"],
                                                            null
                                                            );

            this.ApplicationBar.BackgroundColor = appBarBrush.Color;

#if SCREENSHOT
            SystemTray.IsVisible = false;
#endif
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            busArrivalUpdateTimer.Start();
            viewModel.RegisterEventHandlers(Dispatcher);

            viewModel.CurrentViewState.PropertyChanged += CurrentViewState_PropertyChanged;

            UpdateAppBar(true);

            if (isFiltered == true)
            {
                viewModel.LoadArrivalsForStop(viewModel.CurrentViewState.CurrentStop, viewModel.CurrentViewState.CurrentRoute);
            }
            else
            {
                viewModel.LoadArrivalsForStop(viewModel.CurrentViewState.CurrentStop, null);
            }
        }

        private void DetailsMap_Loaded(object sender, RoutedEventArgs e)
        {
            // When we enter this page after tombstoning often the location won't be available when the map
            // data binding queries CurrentLocationSafe.  The center doesn't update when the property changes
            // so we need to explicitly set the center once the location is known.
            viewModel.LocationTracker.RunWhenLocationKnown(delegate(GeoCoordinate location)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    DetailsMap.Center = location;

                    //calculate distance to current stop and zoom map
                    if (viewModel.CurrentViewState.CurrentStop != null)
                    {
                        if (location.IsUnknown ||
                            location.Latitude < -90.0 || location.Latitude > 90 ||
                            location.Longitude < -180.0 || location.Longitude > 180)
                        {
                            // location is bogus.  don't try to set the map view with it.
                        }
                        else
                        {
                            GeoCoordinate stoplocation = new GeoCoordinate(viewModel.CurrentViewState.CurrentStop.coordinate.Latitude,
                                viewModel.CurrentViewState.CurrentStop.coordinate.Longitude);
                            double radius = 2 * location.GetDistanceTo(stoplocation) * 0.009 * 0.001; // convert metres to degrees and double
                            radius = Math.Max(radius, minimumZoomRadius);
                            radius = Math.Min(radius, maximumZoomRadius);

                            DetailsMap.SetView(new LocationRectangle(location, radius, radius));
                        }
                    }

                    DetailsMap_MapZoom(this, null);
                    RenderPolyline();
                });
            }
            );
        }

        // Only want to use the state variable on the initial call
        void UpdateAppBar(bool useStateVariable)
        {
            bool addFilterButton = false;
            if (useStateVariable == true &&
                PhoneApplicationService.Current.State.ContainsKey(isFilteredStateId) == true 
                && viewModel.CurrentViewState.CurrentRouteDirection != null)
            {
                // This page was tombstoned and is now reloading, use the previous filter status.
                isFiltered = (bool)PhoneApplicationService.Current.State[isFilteredStateId];
                addFilterButton = true;
            }
            else
            {
                // No filter override, this is the first load of this details page. If
                // there is a specific route direction filter based on it and add the 
                // filter button, otherwise we are just displaying a stop, don't show
                // the filter button.
                isFiltered = viewModel.CurrentViewState.CurrentRouteDirection != null;
                addFilterButton = isFiltered;
            }

            if (addFilterButton == true)
            {
                if (appbar_allroutes == null)
                {
                    appbar_allroutes = new ApplicationBarIconButton();
                    appbar_allroutes.Click += new EventHandler(appbar_allroutes_Click);
                }

                if (isFiltered == true)
                {
                    appbar_allroutes.IconUri = unfilterRoutesIcon;
                    appbar_allroutes.Text = unfilterRoutesText;
                }
                else
                {
                    appbar_allroutes.IconUri = filterRoutesIcon;
                    appbar_allroutes.Text = filterRoutesText;
                }

                if (!ApplicationBar.Buttons.Contains(appbar_allroutes))
                {
                    // this has to be done after setting the icon
                    ApplicationBar.Buttons.Add(appbar_allroutes);
                }
            }

            FavoriteRouteAndStop currentInfo = new FavoriteRouteAndStop();
            currentInfo.route = viewModel.CurrentViewState.CurrentRoute;
            currentInfo.routeStops = new RouteStops(viewModel.CurrentViewState.CurrentRouteDirection);
            currentInfo.stop = viewModel.CurrentViewState.CurrentStop;

            isFavorite = viewModel.IsFavorite(currentInfo);
            SetFavoriteIcon();
        }

        void busArrivalUpdateTimer_Tick(object sender, EventArgs e)
        {
            viewModel.RefreshArrivalsForStop(viewModel.CurrentViewState.CurrentStop);
        }

        void DetailsPage_Loaded(object sender, RoutedEventArgs e)
        {
            RecentRouteAndStop recent = new RecentRouteAndStop();
            recent.route = viewModel.CurrentViewState.CurrentRoute;
            recent.routeStops = new RouteStops(viewModel.CurrentViewState.CurrentRouteDirection);
            recent.stop = viewModel.CurrentViewState.CurrentStop;

            ArrivalsListBox.DataContext = viewModel;
            TitleGrid.DataContext = viewModel;

            viewModel.AddRecent(recent);
        }

        void DetailsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            // clear binding references to allow them to get garbage collected.
            // anything bound to CurrentViewState needs to be cleared.
            this.RouteInfo.DataContext = null;
            this.RouteName.DataContext = null;
            this.RouteNumber.DataContext = null;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            busArrivalUpdateTimer.Stop();
            PhoneApplicationService.Current.State[isFilteredStateId] = isFiltered;

            viewModel.CurrentViewState.PropertyChanged -= CurrentViewState_PropertyChanged;
            viewModel.UnregisterEventHandlers();

            RouteInfo.DataContext = null;
        }

        void DetailsPage_BackKeyPress(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (popup != null && popup.IsOpen == true)
            {
                popup.IsOpen = false;
                e.Cancel = true;
            }
            else
            {
                e.Cancel = false;
            }
        }

        private void appbar_favorite_Click(object sender, EventArgs e)
        {
            FavoriteRouteAndStop favorite = new FavoriteRouteAndStop();
            favorite.route = viewModel.CurrentViewState.CurrentRoute;
            favorite.stop = viewModel.CurrentViewState.CurrentStop;
            favorite.routeStops = new RouteStops(viewModel.CurrentViewState.CurrentRouteDirection);

            if (isFavorite == false)
            {
                viewModel.AddFavorite(favorite);
                isFavorite = true;
            }
            else
            {
                viewModel.DeleteFavorite(favorite);
                isFavorite = false;
            }

            SetFavoriteIcon();
        }

        private void appbar_allroutes_Click(object sender, EventArgs e)
        {
            if (isFiltered == true)
            {
                viewModel.ChangeFilterForArrivals(null);
                isFiltered = false;
            }
            else
            {
                viewModel.ChangeFilterForArrivals(viewModel.CurrentViewState.CurrentRoute);
                isFiltered = true;
            }

            SetFilterRoutesIcon();
        }

        private void SetFilterRoutesIcon()
        {
            if (isFiltered == false)
            {
                appbar_allroutes.IconUri = filterRoutesIcon;
                appbar_allroutes.Text = filterRoutesText;
            }
            else
            {
                appbar_allroutes.IconUri = unfilterRoutesIcon;
                appbar_allroutes.Text = unfilterRoutesText;
            }
        }

        private void SetFavoriteIcon()
        {
            if (isFavorite == true)
            {
                Dispatcher.BeginInvoke(() =>
                    {
                        appbar_favorite.IconUri = deleteFavoriteIcon;
                        appbar_favorite.Text = deleteFavoriteText;
                    });
            }
            else
            {
                Dispatcher.BeginInvoke(() =>
                    {
                        appbar_favorite.IconUri = addFavoriteIcon;
                        appbar_favorite.Text = addFavoriteText;
                    });
            }
        }

        private void ArrivalsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count != 0)
            {
                ArrivalAndDeparture arrival = (ArrivalAndDeparture)e.AddedItems[0];
                viewModel.SwitchToRouteByArrival(arrival, () => UpdateAppBar(false));
            }
        }

        void CurrentViewState_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // hook changes to the route + direction, so that we can update the route polylines
            if (e.PropertyName.Equals("CurrentRouteDirection"))
            {
                Dispatcher.BeginInvoke(() => RenderPolyline());
            }
        }

        // WP8 Maps SDK doesn't support databindings for polylines.
        // draw them in with code instead.
        private void RenderPolyline()
        {
            DetailsMap.MapElements.Clear();
            if (viewModel.CurrentViewState.CurrentRouteDirection != null)
            {
                RouteStops routeStops = viewModel.CurrentViewState.CurrentRouteDirection;

                if (routeStops.encodedPolylines != null)
                {
                    foreach (PolyLine pl in routeStops.encodedPolylines)
                    {
                        MapPolyline routePolyline = new MapPolyline();
                        routePolyline.StrokeThickness = 5;
                        routePolyline.StrokeColor = ((SolidColorBrush)Application.Current.Resources["OBAAccentBrush"]).Color;
                        pl.Coordinates.ForEach(coordinate => routePolyline.Path.Add(new GeoCoordinate(coordinate.Latitude, coordinate.Longitude)));
                        DetailsMap.MapElements.Add(routePolyline);
                    }
                }
            }
        }

        private void DetailsMap_MapZoom(object sender, MapZoomLevelChangedEventArgs e)
        {
            // this loop is an ugly way to find the stop controls.
            // they're not directly available from the MapItemsControl, so we have to find them in the Map
            // the MapExtensions.GetChildren method only finds us the data objects, not the UI controls
            foreach (MapLayer layer in DetailsMap.Layers)
            {
                foreach (MapOverlay overlay in layer)
                {
                    ContentPresenter item = overlay.Content as ContentPresenter;
                    if (item == null)
                        continue;
                    if (item.Content is Stop)
                    {
                        if (DetailsMap.ZoomLevel < 13.5)
                        {
                            item.Visibility = System.Windows.Visibility.Collapsed;
                        }
                        else
                        {
                            item.Visibility = System.Windows.Visibility.Visible;
                        }
                    }
                }
            }
        }

        private void BusStopPushpin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button)
            {
                string selectedStopId = (string)((Button)sender).Tag;
                Stop selectedStop = null;

                foreach (Stop stop in viewModel.CurrentViewState.CurrentRouteDirection.stops)
                {
                    if (stop != null && stop.id == selectedStopId)
                    {
                        selectedStop = stop;
                        viewModel.SwitchToStop(selectedStop);

                        break;
                    }
                }

                Debug.Assert(selectedStop != null);

            }
        }

        private void appbar_refresh_Click(object sender, EventArgs e)
        {
            if (viewModel.operationTracker.Loading == false && viewModel.CurrentViewState.CurrentStop != null)
            {
                NoResultsTextBlock.Visibility = System.Windows.Visibility.Collapsed;
                viewModel.LoadArrivalsForStop(viewModel.CurrentViewState.CurrentStop);
            }
        }

        private void GestureListener_Hold(object sender, Microsoft.Phone.Controls.GestureEventArgs e)
        {
            ArrivalAndDeparture a = (ArrivalAndDeparture)(((FrameworkElement)sender).DataContext);
            MessageBoxResult r = MessageBox.Show("Notify me when this bus is 5 minutes away?", "Notify me?", MessageBoxButton.OKCancel);
            if (r == MessageBoxResult.OK)
            {
                this.viewModel.SubscribeToToastNotification(a.stopId, a.tripId, 5);
            }
        }

        private void ZoomToBus_Click(object sender, RoutedEventArgs e)
        {
            ArrivalAndDeparture a = (ArrivalAndDeparture)(((FrameworkElement)sender).DataContext);

            if (a.tripDetails != null && a.tripDetails.locationKnown == true && a.tripDetails.coordinate != null)
            {
                GeoCoordinate location = new GeoCoordinate(a.tripDetails.coordinate.Latitude, a.tripDetails.coordinate.Longitude);
                DetailsMap.Center = location;
                DetailsMap.ZoomLevel = 17;
            }
        }

        private void NotifyArrival_Click(object sender, RoutedEventArgs e)
        {
            ArrivalAndDeparture a = (ArrivalAndDeparture)(((FrameworkElement)sender).DataContext);

            this.popup = new Popup();

            NotifyPopup notifyPopup = new NotifyPopup();
            notifyPopup.Notify_Completed += delegate(object o, NotifyEventArgs args) 
            {
                if (args.okSelected == true)
                {
                    this.viewModel.SubscribeToToastNotification(a.stopId, a.tripId, args.minutes);
                }

                // This is how we track if the popup is visible
                Dispatcher.BeginInvoke(() => { this.popup.IsOpen = false; });
            };

            this.popup.Child = notifyPopup; 
            this.popup.IsOpen = true;
        }

        private void ArrivalsListBox_LayoutUpdated(object sender, EventArgs e)
        {
            if((sender != null))
            {
                object arrivals = ArrivalsListBox.DataContext;
            }

        }
    }
}
