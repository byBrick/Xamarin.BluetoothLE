using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using BluetoothLE.Core;

namespace BluetoothLE.Droid.Extensions
{
    public static class ConnectionPriorityExtension
    {
        public static Android.Bluetooth.GattConnectionPriority ToGattConnectionPriority(this ConnectionPriority connectionPriority)
        {
            switch (connectionPriority)
            {
                case ConnectionPriority.Balanced:
                    return GattConnectionPriority.Balanced;
                case ConnectionPriority.LowPower:
                    return GattConnectionPriority.LowPower;
                case ConnectionPriority.Hight:
                    return GattConnectionPriority.High;
                default:
                    throw new ArgumentOutOfRangeException(nameof(connectionPriority), connectionPriority, null);
            }
        }
    }
}