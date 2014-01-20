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
using System.IO.IsolatedStorage;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.ComponentModel;
using System.Device.Location;
using Microsoft.Devices;
using System.Collections.Generic;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using OneBusAway.WP7.ViewModel.EventArgs;
using System.Windows.Threading;

namespace OneBusAway.WP7.ViewModel
{
    public abstract class AViewModel : INotifyPropertyChanged
    {
        #region Constructors

        public AViewModel()
            :   this(null,null,null)
        {

        }

        public AViewModel(IBusServiceModel busServiceModel)
            : this(busServiceModel, null)
        {

        }

        public AViewModel(IBusServiceModel busServiceModel, IAppDataModel appDataModel)
            : this(busServiceModel, appDataModel, null)
        {

        }

        public AViewModel(IBusServiceModel busServiceModel, IAppDataModel appDataModel, ILocationModel locationModel)
        {
            this.lazyBusServiceModel = busServiceModel;
            this.lazyAppDataModel = appDataModel;

	        if (!IsInDesignMode)
	        {
		        locationTracker = new LocationTracker();
		        operationTracker = new AsyncOperationTracker();
	        }

	        // Set up the default action, just execute in the same thread
            UIAction = (uiAction => uiAction());
            
            eventsRegistered = false;
        }

        #endregion

        #region Private/Protected Properties

        // Always use the same search radius so the cache will be much
        // more efficient.  500 m is small enough that we almost never
        // exceed the 100 stop limit, even downtown.
        protected int defaultSearchRadius = 500;

        private IBusServiceModel lazyBusServiceModel;
        protected IBusServiceModel busServiceModel
        {
            get
            {
                if (lazyBusServiceModel == null)
                {
                    lazyBusServiceModel = (IBusServiceModel)Assembly.Load("OneBusAway.WP7.Model")
                        .GetType("OneBusAway.WP7.Model.BusServiceModel")
                        .GetField("Singleton")
                        .GetValue(null);
                    lazyBusServiceModel.Initialize();
                }
                return lazyBusServiceModel;
            }
        }
 
 	 	private IAppDataModel lazyAppDataModel;
 	 	protected IAppDataModel appDataModel
        {
            get
            {
                if (lazyAppDataModel == null)
                {
                    lazyAppDataModel = (IAppDataModel)Assembly.Load("OneBusAway.WP7.Model")
                        .GetType("OneBusAway.WP7.Model.AppDataModel")
                        .GetField("Singleton")
                        .GetValue(null);
                }
                return lazyAppDataModel;
            }
        }

        private ILocationModel lazyLocationModel;
        protected ILocationModel locationModel
        {
            get
            {
                if (lazyLocationModel == null)
                {
                    lazyLocationModel = (ILocationModel)Assembly.Load("OneBusAway.WP7.Model")
                        .GetType("OneBusAway.WP7.Model.LocationModel")
                        .GetField("Singleton")
                        .GetValue(null);
                }
                return lazyLocationModel;
            }
        }

		protected bool IsInDesignMode
		{
			get
			{
				return IsInDesignModeStatic;
			}
		}
  
        /// <summary>
        /// Subclasses should queue and dequeue their async calls onto this object to tie into the Loading property.
        /// </summary>
        public AsyncOperationTracker operationTracker { get; private set; }

        protected LocationTracker locationTracker;

        private bool eventsRegistered;

        #endregion

        #region Private/Protected Methods

        protected void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                UIAction(() =>
                {
                    // Check again in case it has changed while we waited to execute on the UI thread
                    if (PropertyChanged != null)
                    {
                        PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                    }
                }
                );
            }
        }

        #endregion

        #region Public Members

        private Action<Action> uiAction;
        public Action<Action> UIAction
        {
            get { return uiAction; }

            set
            {
                uiAction = value;

                // Set the ViewState's UIAction
                CurrentViewState.UIAction = uiAction;
                locationTracker.UIAction = uiAction;
                operationTracker.UIAction = uiAction;
            }
        }

        public const string FeedbackEmailAddress = "wp7@onebusaway.org";

        public ViewState CurrentViewState
        {
            get
            {
                return ViewState.Instance;
            }
        }

        public LocationTracker LocationTracker 
        { 
            get 
            { 
                return locationTracker; 
            } 
        }

        public event PropertyChangedEventHandler PropertyChanged;

		private static bool? isInDesignModeStatic = null;
		public static bool IsInDesignModeStatic // Convenient method that can be accessed out of an inherited class
		{
			get
			{
				if (isInDesignModeStatic.HasValue)
				{
					// only do the check once and use the last value forever
					return isInDesignModeStatic.Value;
				}
				try
				{
					var isoStor = IsolatedStorageSettings.ApplicationSettings.Contains("asasdasd");
					isInDesignModeStatic = false;
					return isInDesignModeStatic.Value;
				}
				catch (Exception ex)
				{
					// Toss out any errors we get
				}
				// If we get here that means we got an error
				isInDesignModeStatic = true;
				return isInDesignModeStatic.Value;
			}
		}


        /// <summary>
        /// Registers all event handlers with the model.  Call this when 
        /// the page is first loaded.
        /// </summary>
        public virtual void RegisterEventHandlers(Dispatcher dispatcher)
        {
            // Set the UI Actions to occur on the UI thread
            UIAction = (uiAction => dispatcher.BeginInvoke(() => uiAction()));

            locationTracker.Initialize(operationTracker);

            Debug.Assert(eventsRegistered == false);
            eventsRegistered = true;
        }

        /// <summary>
        /// Unregisters all event handlers with the model. Call this when
        /// the page is navigated away from.
        /// </summary>
        public virtual void UnregisterEventHandlers()
        {
            locationTracker.Uninitialize();

            Debug.Assert(eventsRegistered == true);
            eventsRegistered = false;
        }

        #endregion

    }
}
