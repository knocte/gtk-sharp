// ListBase.cs - List base class implementation
//
// Authors: Mike Kestner <mkestner@novell.com>
//
// Copyright (c) 2002 Mike Kestner
// Copyright (c) 2005 Novell, Inc.
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of version 2 of the Lesser GNU General 
// Public License as published by the Free Software Foundation.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this program; if not, write to the
// Free Software Foundation, Inc., 59 Temple Place - Suite 330,
// Boston, MA 02111-1307, USA.


namespace GLib {

	using System;
	using System.Collections;
	using System.Runtime.InteropServices;

	public abstract class ListBase : IDisposable, ICollection, GLib.IWrapper, ICloneable {

		private IntPtr list_ptr = IntPtr.Zero;
		private int length = -1;
		private bool managed = false;
		internal bool elements_owned = false;
		protected System.Type element_type = null;

                abstract internal IntPtr NthData (uint index);
		abstract internal IntPtr GetData (IntPtr current);
		abstract internal IntPtr Next (IntPtr current);
		abstract internal int Length (IntPtr list);
		abstract internal void Free (IntPtr list);
		abstract internal IntPtr Append (IntPtr current, IntPtr raw);
		abstract internal IntPtr Prepend (IntPtr current, IntPtr raw);

		internal ListBase (IntPtr list, System.Type element_type, bool owned, bool elements_owned)
		{
			list_ptr = list;
			this.element_type = element_type;
			managed = owned;
			this.elements_owned = elements_owned;
		}
		
		~ListBase ()
		{
			Dispose (false);
		}
		
		[Obsolete ("Replaced by owned parameter on ctor.")]
		public bool Managed {
			set { managed = value; }
		}
		
		public IntPtr Handle {
			get {
				return list_ptr;
			}
		}

		public void Append (IntPtr raw)
		{
			list_ptr = Append (list_ptr, raw);
		}

		public void Append (string item)
		{
			this.Append (Marshaller.StringToPtrGStrdup (item));
		}

		public void Prepend (IntPtr raw)
		{
			list_ptr = Prepend (list_ptr, raw);
		}

		// ICollection
		public int Count {
			get {
				if (length == -1)
					length = Length (list_ptr);
				return length;
			}
		}

		public object this [int index] { 
			get { 
				IntPtr data = NthData ((uint) index);
				object ret = null;
				ret = DataMarshal (data);
				return ret;
			}
		}

		// Synchronization could be tricky here. Hmm.
		public bool IsSynchronized {
			get { return false; }
		}

		public object SyncRoot {
			get { return null; }
		}

		public void CopyTo (Array array, int index)
		{
			object[] orig = new object[Count];
			int i = 0;
			foreach (object o in this)
				orig[i++] = o;
			
			orig.CopyTo (array, index); 
		}

		public class FilenameString {
			private FilenameString () {}
		}

		internal object DataMarshal (IntPtr data) 
		{
			object ret = null;
			if (element_type != null) {
				if (element_type == typeof (string))
					ret = Marshaller.Utf8PtrToString (data);
				else if (element_type == typeof (FilenameString))
					ret = Marshaller.FilenamePtrToString (data);
				else if (element_type == typeof (IntPtr))
					ret = data;
				else if (element_type.IsSubclassOf (typeof (GLib.Object)))
					ret = GLib.Object.GetObject (data, false);
				else if (element_type.IsSubclassOf (typeof (GLib.Opaque)))
					ret = GLib.Opaque.GetOpaque (data, element_type, elements_owned);
				else if (element_type == typeof (int))
					ret = (int) data;
				else if (element_type.IsValueType)
					ret = Marshal.PtrToStructure (data, element_type);
				else
					ret = Activator.CreateInstance (element_type, new object[] {data});

			} else if (Object.IsObject (data))
				ret = GLib.Object.GetObject (data, false);

			return ret;
		}

		[DllImport ("libglib-2.0-0.dll")]
		static extern void g_free (IntPtr item);

		[DllImport ("libglib-2.0-0.dll")]
		static extern void g_object_unref (IntPtr item);

		public void Empty ()
		{
			if (elements_owned)
				for (uint i = 0; i < Count; i++)
					if (typeof (GLib.Object).IsAssignableFrom (element_type))
						g_object_unref (NthData (i));
					else if (typeof (GLib.Opaque).IsAssignableFrom (element_type))
						GLib.Opaque.GetOpaque (NthData (i), element_type, true).Dispose ();
					else 
						g_free (NthData (i));

			if (managed)
				FreeList ();
		}

		private class ListEnumerator : IEnumerator
		{
			private IntPtr current = IntPtr.Zero;
			private ListBase list;

			public ListEnumerator (ListBase list)
			{
				this.list = list;
			}

			public object Current {
				get {
					IntPtr data = list.GetData (current);
					object ret = null;
					ret = list.DataMarshal (data);
					return ret;
				}
			}

			public bool MoveNext ()
			{
				if (current == IntPtr.Zero)
					current = list.list_ptr;
				else
					current = list.Next (current);
				return (current != IntPtr.Zero);
			}

			public void Reset ()
			{
				current = IntPtr.Zero;
			}
		}
		
		// IEnumerable
		public IEnumerator GetEnumerator ()
		{
			return new ListEnumerator (this);
		}

		// IDisposable
		public void Dispose ()
		{
			Dispose (true);
			GC.SuppressFinalize (this);
		}

		protected virtual void Dispose (bool disposing)
		{
			Empty ();
		}
		
		void FreeList ()
		{
			if (list_ptr != IntPtr.Zero)
				Free (list_ptr);
			list_ptr = IntPtr.Zero;
			length = -1;
		}

		// ICloneable
		abstract public object Clone ();
	}
}