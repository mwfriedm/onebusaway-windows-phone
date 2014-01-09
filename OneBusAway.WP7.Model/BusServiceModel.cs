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
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using OneBusAway.WP7.ViewModel;
using System.Device.Location;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using OneBusAway.WP7.ViewModel.BusServiceDataStructures;
using System.Diagnostics;
using OneBusAway.WP7.ViewModel.EventArgs;
using Microsoft.Phone.Controls.Maps;
using OneBusAway.WP7.ViewModel.LocationServiceDataStructures;

namespace OneBusAway.WP7.Model
{
    public class BusServiceModel : IBusServiceModel
    {
        private OneBusAwayWebservice webservice;

        #region Events

        public event EventHandler<CombinedInfoForLocationEventArgs> CombinedInfoForLocation_Completed;
        public event EventHandler<StopsForLocationEventArgs> StopsForLocation_Completed;
        public event EventHandler<RoutesForLocationEventArgs> RoutesForLocation_Completed;
        public event EventHandler<StopsForRouteEventArgs> StopsForRoute_Completed;
        public event EventHandler<ArrivalsForStopEventArgs> ArrivalsForStop_Completed;
        public event EventHandler<ScheduleForStopEventArgs> ScheduleForStop_Completed;
        public event EventHandler<TripDetailsForArrivalEventArgs> TripDetailsForArrival_Completed;
        public event EventHandler<SearchForRoutesEventArgs> SearchForRoutes_Completed;
        public event EventHandler<SearchForStopsEventArgs> SearchForStops_Completed;
        public event EventHandler<LocationForAddressEventArgs> LocationForAddress_Completed;


        #endregion

        #region Constructor/Singleton

        // TODO we really need to get rid of this singleton and move to a better dependency injection model at some point.

        public static BusServiceModel Singleton = new BusServiceModel();

        private BusServiceModel()
        {
        }

        #endregion

        /// <summary>
        /// Scan the list of stops to find all associated routes.
        /// </summary>
        /// <param name="stops"></param>
        /// <param name="location">Center location used to find closestStop on each route.</param>
        /// <returns></returns>
        private List<Route> GetRoutesFromStops(List<Stop> stops, GeoCoordinate location)
        {
            IDictionary<string, Route> routesMap = new Dictionary<string, Route>();
            stops.Sort(new StopDistanceComparer(location));

            foreach (Stop stop in stops)
            {
                foreach (Route route in stop.routes)
                {
                    if (!routesMap.ContainsKey(route.id))
                    {
                        // the stops are sorted in distance order.
                        // so if we haven't already seen this route, then this is the closest stop.
                        route.closestStop = stop;
                        routesMap.Add(route.id, route);
                    }
                }
            }
            return routesMap.Values.ToList<Route>();
        }

        #region Public Methods

        public void Initialize()
        {
            webservice = new OneBusAwayWebservice();
        }

        public double DistanceFromClosestSupportedRegion(GeoCoordinate location)
        {
            return OneBusAwayWebservice.ClosestRegion(location).DistanceFrom(location.Latitude, location.Longitude);
        }

        public bool AreLocationsEquivalent(GeoCoordinate location1, GeoCoordinate location2)
        {
            return OneBusAwayWebservice.GetRoundedLocation(location1) == OneBusAwayWebservice.GetRoundedLocation(location2);
        }

        public void CombinedInfoForLocation(GeoCoordinate location, int radiusInMeters)
        {
            CombinedInfoForLocation(location, radiusInMeters, -1);
        }

        public void CombinedInfoForLocation(GeoCoordinate location, int radiusInMeters, int maxCount)
        {
            CombinedInfoForLocation(location, radiusInMeters, maxCount, false);
        }

        public void CombinedInfoForLocation(GeoCoordinate location, int radiusInMeters, int maxCount, bool invalidateCache)
        {
            webservice.StopsForLocation(
                location,
                null,
                radiusInMeters,
                maxCount,
                invalidateCache,
                delegate(List<Stop> stops, bool limitExceeded)
                {
                    List<Route> routes = new List<Route>();

                    routes = GetRoutesFromStops(stops, location);

                    if (CombinedInfoForLocation_Completed != null)
                    {
                        CombinedInfoForLocation_Completed(this, new ViewModel.EventArgs.CombinedInfoForLocationEventArgs(stops, routes, location));
                    }
                }
            );
        }

        public void StopsForLocation(GeoCoordinate location, int radiusInMeters)
        {
            StopsForLocation(location, radiusInMeters, -1);
        }

        public void StopsForLocation(GeoCoordinate location, int radiusInMeters, int maxCount)
        {
            StopsForLocation(location, radiusInMeters, maxCount, false);
        }

        public void StopsForLocation(GeoCoordinate location, int radiusInMeters, int maxCount, bool invalidateCache)
        {
            webservice.StopsForLocation(
                location,
                null,
                radiusInMeters,
                maxCount,
                invalidateCache,
                delegate(List<Stop> stops, bool limitExceeded)
                {
                    if (StopsForLocation_Completed != null)
                    {
                        StopsForLocation_Completed(this, new ViewModel.EventArgs.StopsForLocationEventArgs(stops, location, limitExceeded));
                    }
                }
            );
        }

        public void RoutesForLocation(GeoCoordinate location, int radiusInMeters)
        {
            RoutesForLocation(location, radiusInMeters, -1);
        }

        public void RoutesForLocation(GeoCoordinate location, int radiusInMeters, int maxCount)
        {
            RoutesForLocation(location, radiusInMeters, maxCount, false);
        }

        public void RoutesForLocation(GeoCoordinate location, int radiusInMeters, int maxCount, bool invalidateCache)
        {
            webservice.StopsForLocation(
                location,
                null,
                radiusInMeters,
                maxCount,
                invalidateCache,
                delegate(List<Stop> stops, bool limitExceeded)
                {
                    List<Route> routes = new List<Route>();

                    routes = GetRoutesFromStops(stops, location);

                    if (RoutesForLocation_Completed != null)
                    {
                        RoutesForLocation_Completed(this, new ViewModel.EventArgs.RoutesForLocationEventArgs(routes, location));
                    }
                }
            );
        }

        public void StopsForRoute(GeoCoordinate location, Route route)
        {
            webservice.StopsForRoute(
                location,
                route,
                delegate(List<RouteStops> routeStops)
                {
                    if (StopsForRoute_Completed != null)
                    {
                        StopsForRoute_Completed(this, new ViewModel.EventArgs.StopsForRouteEventArgs(route, routeStops));
                    }
                }
            );
        }

        public void ArrivalsForStop(GeoCoordinate location, Stop stop)
        {
            webservice.ArrivalsForStop(
                location,
                stop,
                delegate(List<ArrivalAndDeparture> arrivals)
                {
                    if (ArrivalsForStop_Completed != null)
                    {
                        ArrivalsForStop_Completed(this, new ViewModel.EventArgs.ArrivalsForStopEventArgs(stop, arrivals));
                    }
                }
            );
        }

        public void ScheduleForStop(GeoCoordinate location, Stop stop)
        {
            webservice.ScheduleForStop(
                location,
                stop,
                delegate(List<RouteSchedule> schedule)
                {
                    if (ScheduleForStop_Completed != null)
                    {
                        ScheduleForStop_Completed(this, new ViewModel.EventArgs.ScheduleForStopEventArgs(stop, schedule));
                    }
                }
            );
        }

        public void TripDetailsForArrivals(GeoCoordinate location, List<ArrivalAndDeparture> arrivals)
        {
            int count = 0;
            List<TripDetails> tripDetails = new List<TripDetails>(arrivals.Count);

            if (arrivals.Count == 0)
            {
                if (TripDetailsForArrival_Completed != null)
                {
                    TripDetailsForArrival_Completed(
                        this,
                        new ViewModel.EventArgs.TripDetailsForArrivalEventArgs(arrivals, tripDetails)
                        );
                }
            }
            else
            {
                arrivals.ForEach(arrival =>
                    {
                        webservice.TripDetailsForArrival(
                            location,
                            arrival,
                            delegate(TripDetails tripDetail)
                            {
                                tripDetails.Add(tripDetail);

                                // Is this code thread-safe?
                                count++;
                                if (count == arrivals.Count && TripDetailsForArrival_Completed != null)
                                {
                                    TripDetailsForArrival_Completed(this, new ViewModel.EventArgs.TripDetailsForArrivalEventArgs(arrivals, tripDetails));
                                }
                            }
                        );
                    }
                );
            }
        }

        public void SearchForRoutes(GeoCoordinate location, string query)
        {
            SearchForRoutes(location, query, 1000000, -1);
        }

        public void SearchForRoutes(GeoCoordinate location, string query, int radiusInMeters, int maxCount)
        {
            webservice.RoutesForLocation(
                location,
                query,
                radiusInMeters,
                maxCount,
                delegate(List<Route> routes)
                {
                    if (SearchForRoutes_Completed != null)
                    {
                        SearchForRoutes_Completed(this, new ViewModel.EventArgs.SearchForRoutesEventArgs(routes, location, query));
                    }
                }
            );
        }

        public void SearchForStops(GeoCoordinate location, string query)
        {
            SearchForStops(location, query, 1000000, -1);
        }

        public void SearchForStops(GeoCoordinate location, string query, int radiusInMeters, int maxCount)
        {
            webservice.StopsForLocation(
                location,
                query,
                radiusInMeters,
                maxCount,
                false,
                delegate(List<Stop> stops, bool limitExceeded)
                {
                    if (SearchForStops_Completed != null)
                    {
                        SearchForStops_Completed(this, new ViewModel.EventArgs.SearchForStopsEventArgs(stops, location, query));
                    }
                }
            );
        }

        public void LocationForAddress(string query, GeoCoordinate searchNearLocation)
        {
            string bingMapAPIURL = "http://dev.virtualearth.net/REST/v1/Locations";
            string requestUrl = string.Format(
                "{0}?query={1}&key={2}&o=xml&userLocation={3}",
                bingMapAPIURL,
                query.Replace('&', ' '),
                "AtAv-npPzjiTyL6ij1J5cgR7Cxmt6h8e3fHlsTSlfWshc8GQ1jfQB1PnB1VfvBGz",
                string.Format("{0},{1}", searchNearLocation.Latitude, searchNearLocation.Longitude)
            );

            WebClient client = new WebClient();
            client.DownloadStringCompleted += new DownloadStringCompletedEventHandler(
                new GetLocationForAddressCompleted(requestUrl,
                        delegate(List<LocationForQuery> locations, Exception error)
                        {
                            if (LocationForAddress_Completed != null)
                            {
                                LocationForAddress_Completed(this, new ViewModel.EventArgs.LocationForAddressEventArgs(
                                        locations,
                                        query,
                                        searchNearLocation,
                                        error
                                        ));
                            }
                        }
                    ).LocationForAddress_Completed);
            client.DownloadStringAsync(new Uri(requestUrl));
        }

        public delegate void LocationForAddress_Callback(List<LocationForQuery> locations, Exception error);
        private class GetLocationForAddressCompleted
        {
            private LocationForAddress_Callback callback;
            private string requestUrl;

            public GetLocationForAddressCompleted(string requestUrl, LocationForAddress_Callback callback)
            {
                this.callback = callback;
                this.requestUrl = requestUrl;
            }

            public void LocationForAddress_Completed(object sender, DownloadStringCompletedEventArgs e)
            {
                Exception error = e.Error;
                List<LocationForQuery> locations = null;
                
                try
                {
                    if (error == null)
                    {
                        XDocument xmlDoc = XDocument.Load(new StringReader(e.Result));

                        XNamespace ns = "http://schemas.microsoft.com/search/local/ws/rest/v1";

                        locations = (from location in xmlDoc.Descendants(ns + "Location")
                               select new LocationForQuery
                               {
                                   location = new GeoCoordinate(
                                       Convert.ToDouble(location.Element(ns + "Point").Element(ns + "Latitude").Value),
                                       Convert.ToDouble(location.Element(ns + "Point").Element(ns + "Longitude").Value)
                                       ),
                                    name = location.Element(ns + "Name").Value,
                                    confidence = (Confidence)Enum.Parse(
                                        typeof(Confidence),
                                        location.Element(ns + "Confidence").Value,
                                        true
                                        ),
                                   boundingBox = new Microsoft.Phone.Maps.Controls.LocationRectangle(
                                        Convert.ToDouble(location.Element(ns + "BoundingBox").Element(ns + "NorthLatitude").Value),
                                        Convert.ToDouble(location.Element(ns + "BoundingBox").Element(ns + "WestLongitude").Value),
                                        Convert.ToDouble(location.Element(ns + "BoundingBox").Element(ns + "SouthLatitude").Value),
                                        Convert.ToDouble(location.Element(ns + "BoundingBox").Element(ns + "EastLongitude").Value)
                                        )
                               }).ToList();

                    }
                }
                catch (Exception ex)
                {
                    error = new WebserviceParsingException(requestUrl, e.Result, ex);
                }

                Debug.Assert(error == null);

                callback(locations, error);
            }
        }

        public void ClearCache()
        {
            // TODO
        }

        #endregion

    }
}
