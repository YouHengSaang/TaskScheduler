﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace System.Runtime.InteropServices
{
	internal class SafeHGlobalHandle : Microsoft.Win32.GenericSafeHandle
	{
		/// <summary>
		/// Maintains reference to other SafeHGlobalHandle objects, the pointer
		/// to which are referred to by this object. This is to ensure that such
		/// objects being referred to wouldn't be unreferenced until this object
		/// is active.
		/// </summary>
		List<SafeHGlobalHandle> references;

		public SafeHGlobalHandle() : this(IntPtr.Zero, 0) { }

		public SafeHGlobalHandle(IntPtr handle, int size, bool ownsHandle = true) :
			base(handle, p => { Marshal.FreeHGlobal(p); return true; }, ownsHandle)
		{
			Size = size;
		}

		public SafeHGlobalHandle(int size) : this()
		{
			if (size < 0)
				throw new ArgumentOutOfRangeException(nameof(size), "The value of this argument must be non-negative");
			System.Runtime.CompilerServices.RuntimeHelpers.PrepareConstrainedRegions();
			SetHandle(Marshal.AllocHGlobal(size));
			Size = size;
		}

		public SafeHGlobalHandle(object value) : this(Marshal.SizeOf(value))
		{
			Marshal.StructureToPtr(value, handle, false);
		}

		/// <summary>
		/// Allocates from unmanaged memory to represent an array of pointers
		/// and marshals the unmanaged pointers (IntPtr) to the native array
		/// equivalent.
		/// </summary>
		/// <param name="values">Array of unmanaged pointers</param>
		/// <returns>SafeHGlobalHandle object to an native (unmanaged) array of pointers</returns>
		public SafeHGlobalHandle(IntPtr[] values) : this(IntPtr.Size * values.Length)
		{
			Marshal.Copy(values, 0, handle, values.Length);
		}

		/// <summary>
		/// Allocates from unmanaged memory to represent a unicode string (WSTR)
		/// and marshal this to a native PWSTR.
		/// </summary>
		/// <param name="s">String</param>
		/// <returns>SafeHGlobalHandle object to an native (unmanaged) unicode string</returns>
		public SafeHGlobalHandle(string s) : this(s == null ? IntPtr.Zero : Marshal.StringToHGlobalUni(s), s == null ? 0 : (s.Length + 1) * 2)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="SafeHGlobalHandle"/> class.
		/// </summary>
		/// <param name="s">The secure string.</param>
		public SafeHGlobalHandle(Security.SecureString s) :
			base(IntPtr.Zero, p => { Marshal.ZeroFreeGlobalAllocUnicode(p); return true; }, true)
		{
			if (s != null)
			{
				s.MakeReadOnly();
				SetHandle(Marshal.SecureStringToGlobalAllocUnicode(s));
				Size = s.Length;
			}
		}

		/// <summary>
		/// Allocates from unmanaged memory sufficient memory to hold an object of type T.
		/// </summary>
		/// <typeparam name="T">Native type</typeparam>
		/// <returns>SafeHGlobalHandle object to an native (unmanaged) memory block the size of T.</returns>
		public static SafeHGlobalHandle AllocHGlobal<T>() => new SafeHGlobalHandle(Marshal.SizeOf(typeof(T)));

		/// <summary>
		/// Allocates from unmanaged memory to represent an array of structures
		/// and marshals the structure elements to the native array of
		/// structures. ONLY structures with attribute StructLayout of
		/// LayoutKind.Sequential are supported.
		/// </summary>
		/// <typeparam name="T">Native structure type</typeparam>
		/// <param name="values">Collection of structure objects</param>
		/// <param name="count">Number of elements in the collection</param>
		/// <returns>SafeHGlobalHandle object to an native (unmanaged) array of structures</returns>
		public static SafeHGlobalHandle AllocHGlobal<T>(ICollection<T> values) where T : struct
		{
			Debug.Assert(typeof(T).StructLayoutAttribute.Value == LayoutKind.Sequential);

			return AllocHGlobal(0, values, values.Count);
		}

		/// <summary>
		/// Allocates from unmanaged memory to represent a structure with a
		/// variable length array at the end and marshal these structure
		/// elements. It is the callers responsibility to marshal what precedes
		/// the trailing array into the unmanaged memory. ONLY structures with
		/// attribute StructLayout of LayoutKind.Sequential are supported.
		/// </summary>
		/// <typeparam name="T">Type of the trailing array of structures</typeparam>
		/// <param name="prefixBytes">Number of bytes preceding the trailing array of structures</param>
		/// <param name="values">Collection of structure objects</param>
		/// <param name="count"></param>
		/// <returns>SafeHGlobalHandle object to an native (unmanaged) structure
		/// with a trail array of structures</returns>
		public static SafeHGlobalHandle AllocHGlobal<T>(int prefixBytes, IEnumerable<T> values, int count) where T : struct
		{
			Debug.Assert(typeof(T).StructLayoutAttribute.Value == LayoutKind.Sequential);

			SafeHGlobalHandle result = new SafeHGlobalHandle(prefixBytes + Marshal.SizeOf(typeof(T)) * count);
			IntPtr ptr = new IntPtr(result.handle.ToInt32() + prefixBytes);
			foreach (var value in values)
			{
				Marshal.StructureToPtr(value, ptr, false);
				ptr = new IntPtr(ptr.ToInt32() + Marshal.SizeOf(typeof(T)));
			}
			return result;
		}

		/// <summary>
		/// Allocates from unmanaged memory to hold a copy of a structure.
		/// </summary>
		/// <typeparam name="T">Type of the structure.</typeparam>
		/// <param name="obj">The object.</param>
		/// <returns>SafeHGlobalHandle object to an native (unmanaged) structure</returns>
		public static SafeHGlobalHandle AllocHGlobalStruct<T>(T obj) where T : struct
		{
			Debug.Assert(typeof(T).StructLayoutAttribute.Value == LayoutKind.Sequential);

			SafeHGlobalHandle result = new SafeHGlobalHandle(Marshal.SizeOf(typeof(T)));
			Marshal.StructureToPtr(obj, result.handle, false);
			return result;
		}

		/// <summary>
		/// Gets the size of the allocated memory block.
		/// </summary>
		/// <value>
		/// The sizeof the allocated memory block.
		/// </value>
		public int Size { get; }

		/// <summary>
		/// Allows to assign IntPtr to SafeHGlobalHandle
		/// </summary>
		public static implicit operator SafeHGlobalHandle(IntPtr ptr) => new SafeHGlobalHandle(ptr, 0, true);

		/// <summary>
		/// Allows to use SafeHGlobalHandle as IntPtr
		/// </summary>
		public static implicit operator IntPtr(SafeHGlobalHandle h) => h.DangerousGetHandle();

		/// <summary>
		/// Adds reference to other SafeHGlobalHandle objects, the pointer to
		/// which are referred to by this object. This is to ensure that such
		/// objects being referred to wouldn't be unreferenced until this object
		/// is active.
		///
		/// For e.g. when this object is an array of pointers to other objects
		/// </summary>
		/// <param name="children">Collection of SafeHGlobalHandle objects referred to by this object.</param>
		public void AddSubReference(IEnumerable<SafeHGlobalHandle> children)
		{
			if (references == null)
				references = new List<SafeHGlobalHandle>();
			references.AddRange(children);
		}

		public T ToStructure<T>() where T : struct
		{
			if (IsInvalid)
				return default(T);
			if (Size < Marshal.SizeOf(typeof(T)))
				throw new InsufficientMemoryException("Requested structure is larger than the memory allocated.");
			return (T)Marshal.PtrToStructure(handle, typeof(T));
		}

		public T[] ToArray<T>(int count, int prefixBytes = 0) where T : struct
		{
			if (IsInvalid)
				return null;

			Debug.Assert(typeof(T).StructLayoutAttribute.Value == LayoutKind.Sequential);

			T[] ret = new T[count];
			IntPtr ptr = new IntPtr(handle.ToInt32() + prefixBytes);
			for (int i = 0; i < count; i++)
			{
				ret[i] = (T)Marshal.PtrToStructure(ptr, typeof(T));
				ptr = new IntPtr(ptr.ToInt32() + Marshal.SizeOf(typeof(T)));
			}
			return ret;
		}
	}
}
