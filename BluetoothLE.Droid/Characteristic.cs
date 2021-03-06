﻿using System;
using BluetoothLE.Core;
using Android.Bluetooth;
using System.Linq;
using System.Threading;
using BluetoothLE.Core.Events;
using BluetoothLE.Core.Exceptions;
using Java.Util;

namespace BluetoothLE.Droid {
	/// <summary>
	/// Concrete implmentation of <see cref="BluetoothLE.Core.ICharacteristic" /> interface
	/// </summary>
	public class Characteristic : ICharacteristic {
		private readonly BluetoothGattCharacteristic _nativeCharacteristic;
		private readonly BluetoothGatt _gatt;
		private readonly GattCallback _callback;
		private bool _isUpdating;

		public Characteristic(Guid uuid, CharacterisiticPermissionType permissions, CharacteristicPropertyType properties) {
			GattPermission gattPermissions = GetNativePermissions(permissions);

			_nativeCharacteristic = new BluetoothGattCharacteristic(UUID.FromString(uuid.ToString()), (GattProperty)properties, gattPermissions);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="BluetoothLE.Droid.Characteristic"/> class.
		/// </summary>
		/// <param name="characteristic">Characteristic.</param>
		/// <param name="gatt">Gatt.</param>
		/// <param name="gattCallback">Gatt callback.</param>
		public Characteristic(BluetoothGattCharacteristic characteristic, BluetoothGatt gatt, GattCallback gattCallback) {
			_nativeCharacteristic = characteristic;
			_gatt = gatt;
			_callback = gattCallback;

			if (_callback != null) {
				_callback.CharacteristicValueUpdated += OnCharacteristicValueUpdated;
				_callback.CharacteristicWriteComplete += OnCharacteristicWriteComplete;
				_callback.DescriptorWriteComplete += OnDescriptorWriteComplete;
			}
		}

		#region ICharacteristic implementation

		/// <summary>
		/// Occurs when value updated.
		/// </summary>
		public event EventHandler<CharacteristicUpdateEventArgs> ValueUpdated = delegate { };

		public event EventHandler<CharacteristicNotificationStateEventArgs> NotificationStateChanged;

		public event EventHandler<CharacteristicWriteEventArgs> WriteComplete;

		public event EventHandler<DescriptorWriteEventArgs> DescriptorWriteComplete;

		public bool Updating => _isUpdating;

	    /// <summary>
		/// Subscribe to the characteristic
		/// </summary>
		public void StartUpdates() {
			if (!CanUpdate)
				throw new InvalidOperationException("Characteristic does not support UPDATE");

			SetUpdateValue(true);
		}

	    /// <summary>
		/// Unsubscribe from the characteristic
		/// </summary>
		public void StopUpdates() {
			if (CanUpdate)
				SetUpdateValue(false);
		}

	    /// <summary>
		/// Read the characteristic's value
		/// </summary>
		public void Read() {
			if (!CanRead)
				throw new InvalidOperationException("Characteristic does not support READ");
			_gatt.ReadCharacteristic(_nativeCharacteristic);
		}

	    /// <summary>
		/// Write the specified data to the characteristic
		/// </summary>
		/// <param name="data">Data.</param>
		public void Write(byte[] data, CharacteristicWriteType writeType) {
			if (!CanWrite) {
				throw new InvalidOperationException("Characteristic does not support WRITE");
			}

			_nativeCharacteristic.SetValue(data);

			if (writeType == CharacteristicWriteType.WithResponse) {
				_nativeCharacteristic.WriteType = GattWriteType.Default;
			} else if (writeType == CharacteristicWriteType.WithoutResponse) {
				_nativeCharacteristic.WriteType = GattWriteType.NoResponse;
			}

			var success = _gatt.WriteCharacteristic(_nativeCharacteristic);

			if (!success) {
				WriteComplete?.Invoke(this, new CharacteristicWriteEventArgs(false, this));
				//throw new CharacteristicException("Write failed", CharacteristicException.Code.WriteFailed);
			}
		}

	    /// <summary>
		/// Gets the unique identifier.
		/// </summary>
		/// <value>The unique identifier.</value>
		public Guid Id {
			get { return Guid.Parse(_nativeCharacteristic.Uuid.ToString()); }
		}

	    /// <summary>
		/// Gets the UUID.
		/// </summary>
		/// <value>The UUID.</value>
		public string Uuid {
			get { return _nativeCharacteristic.Uuid.ToString(); }
		}

	    /// <summary>
		/// Gets the characteristic's value.
		/// </summary>
		/// <value>The characteristic's value.</value>
		public byte[] Value {
			get { return _nativeCharacteristic.GetValue(); }
			set { _nativeCharacteristic.SetValue(value); }
		}

	    /// <summary>
		/// Gets the characteristic's value as a string.
		/// </summary>
		/// <value>The characteristic's value, interpreted as a string.</value>
		public string StringValue {
			get {
				var val = Value;
				if (val == null)
					return string.Empty;

				return System.Text.Encoding.UTF8.GetString(val);
			}
		}

	    /// <summary>
		/// Gets the native characteristic. Should be cast to the appropriate type.
		/// </summary>
		/// <value>The native characteristic.</value>
		public object NativeCharacteristic {
			get { return _nativeCharacteristic; }
		}

	    /// <summary>
		/// Gets the characteristic's properties
		/// </summary>
		/// <value>The characteristic's properties.</value>
		public CharacteristicPropertyType Properties {
			get { return (CharacteristicPropertyType)(int)_nativeCharacteristic.Properties; }
		}

	    public CharacterisiticPermissionType Permissions {
			get {
				return GetPermissions(_nativeCharacteristic.Permissions);
			}
		}

	    /// <summary>
		/// Gets a value indicating whether this instance can read.
		/// </summary>
		/// <value>true</value>
		/// <c>false</c>
		public bool CanRead {
			get { return (Properties & CharacteristicPropertyType.Read) > 0; }
		}

	    /// <summary>
		/// Gets a value indicating whether this instance can update.
		/// </summary>
		/// <value>true</value>
		/// <c>false</c>
		public bool CanUpdate {
			get {
				return (Properties & CharacteristicPropertyType.Notify) > 0 || (Properties & CharacteristicPropertyType.Indicate) > 0;

			}
		}

	    /// <summary>
		/// Gets a value indicating whether this instance can write.
		/// </summary>
		/// <value>true</value>
		/// <c>false</c>
		public bool CanWrite {
			get { return Properties.HasFlag(CharacteristicPropertyType.WriteWithoutResponse) || Properties.HasFlag(CharacteristicPropertyType.Write); }
		}

	    #endregion

	    #region GattCallback delegate methods

	    private void OnCharacteristicValueUpdated(object sender, CharacteristicUpdateEventArgs e) {
			if (e.Characteristic.Id == this.Id) {
				ValueUpdated(this, e);
			}
		}

	    private void OnCharacteristicWriteComplete(object sender, CharacteristicWriteEventArgs characteristicUpdateEventArgs) {
			if (characteristicUpdateEventArgs.Characteristic.Id == this.Id) {
				WriteComplete?.Invoke(this, new CharacteristicWriteEventArgs(true, this));
			}
		}

	    private void OnDescriptorWriteComplete(object sender, DescriptorWriteEventArgs descriptorWriteEventArgs) {
			if (descriptorWriteEventArgs.CharacteristicId == this.Id) {
                const string descriptorId = "00002902-0000-1000-8000-00805f9b34fb";
			    if (descriptorWriteEventArgs.DescriptorId.ToString() == descriptorId) {
                    NotificationStateChanged?.Invoke(this, new CharacteristicNotificationStateEventArgs(this, true));
                }
				DescriptorWriteComplete?.Invoke(this, descriptorWriteEventArgs);
			}
		}

	    #endregion

	    private void SetUpdateValue(bool enable) {
			var success = _gatt.SetCharacteristicNotification(_nativeCharacteristic, enable);
			if (!success) {
				NotificationStateChanged?.Invoke(this, new CharacteristicNotificationStateEventArgs(this, false));
			}
			
			if (_nativeCharacteristic.Descriptors.Count > 0) {
				const string descriptorId = "00002902-0000-1000-8000-00805f9b34fb";
				var value = enable ? BluetoothGattDescriptor.EnableNotificationValue : BluetoothGattDescriptor.DisableNotificationValue;
				var descriptor = _nativeCharacteristic.Descriptors.FirstOrDefault(x => x.Uuid.ToString() == descriptorId);
				if (descriptor != null && !descriptor.SetValue(value.ToArray())) {
					throw new Exception("Unable to set the notification value on the descriptor");
				}
				var dSuccess =_gatt.WriteDescriptor(descriptor);
				if (!dSuccess) {
					//NotificationStateChanged?.Invoke(this, new CharacteristicNotificationStateEventArgs(this, false));
				}
			}
			_isUpdating = enable;
		}

	    /// <summary>
		/// Convert abstracted permissions to android native permissions
		/// </summary>
		/// <param name="permissions"></param>
		/// <returns></returns>
		GattPermission GetNativePermissions(CharacterisiticPermissionType permissions) {
			GattPermission nativePermissions = 0;
			foreach (CharacterisiticPermissionType value in Enum.GetValues(typeof(CharacterisiticPermissionType))) {
				if (permissions.HasFlag(value)) {
					switch (value) {
						case CharacterisiticPermissionType.Read:
							nativePermissions |= GattPermission.Read;
							break;
						case CharacterisiticPermissionType.Write:
							nativePermissions |= GattPermission.Write;
							break;
						case CharacterisiticPermissionType.ReadEncrypted:
							nativePermissions |= GattPermission.ReadEncrypted;
							break;
						case CharacterisiticPermissionType.WriteEncrypted:
							nativePermissions |= GattPermission.WriteEncrypted;
							break;
					}
				}
			}
			return nativePermissions;
		}

	    public void SetIndication(bool enable) {
	        if (_nativeCharacteristic.Descriptors.Count > 0) {
	            var set = _gatt.SetCharacteristicNotification(_nativeCharacteristic, enable);
	            if (!set) {
	                //DescriptorWriteComplete?.Invoke(this, new DescriptorWriteEventArgs(false, this.Id, "2902".ToGuid()));
	            }

	            string descriptorId = "2902".ToGuid().ToString();
	            var descriptor = _nativeCharacteristic.Descriptors.FirstOrDefault(x => x.Uuid.ToString() == descriptorId);
	            if (descriptor != null) {
	                var value = (enable) ?  BluetoothGattDescriptor.EnableIndicationValue : BluetoothGattDescriptor.DisableNotificationValue;
	                descriptor.SetValue(value.ToArray());
	                var write = _gatt.WriteDescriptor(descriptor);
	                if (!write) {
	                    //DescriptorWriteComplete?.Invoke(this, new DescriptorWriteEventArgs(false, this.Id, "2902".ToGuid()));
	                }
	            }
	        }
	    }

	    /// <summary>
		/// Convert native permissions to abstracted permissions
		/// </summary>
		/// <param name="permission"></param>
		/// <returns></returns>
		CharacterisiticPermissionType GetPermissions(GattPermission permission) {
			CharacterisiticPermissionType t = 0;
			foreach (GattPermission value in Enum.GetValues(typeof(GattPermission))) {
				switch (value) {
					case GattPermission.Read:
						t |= CharacterisiticPermissionType.Read;
						break;
					case GattPermission.ReadEncrypted:
						t |= CharacterisiticPermissionType.ReadEncrypted;
						break;
					case GattPermission.ReadEncryptedMitm:
						t |= CharacterisiticPermissionType.ReadEncryptedMitm;
						break;
					case GattPermission.Write:
						t |= CharacterisiticPermissionType.Write;
						break;
					case GattPermission.WriteEncrypted:
						t |= CharacterisiticPermissionType.WriteEncrypted;
						break;
					case GattPermission.WriteEncryptedMitm:
						t |= CharacterisiticPermissionType.WriteEncryptedMitm;
						break;
					case GattPermission.WriteSigned:
						t |= CharacterisiticPermissionType.WriteSigned;
						break;
					case GattPermission.WriteSignedMitm:
						t |= CharacterisiticPermissionType.WriteSignedMitm;
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
			return t;
		}
	}
}

