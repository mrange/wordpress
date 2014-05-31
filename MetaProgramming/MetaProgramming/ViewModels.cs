// ----------------------------------------------------------------------------------------------
// Copyright (c) Mårten Rånge.
// ----------------------------------------------------------------------------------------------
// This source code is subject to terms and conditions of the Microsoft Public License. A 
// copy of the license can be found in the License.html file at the root of this distribution. 
// If you cannot locate the  Microsoft Public License, please send an email to 
// dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
//  by the terms of the Microsoft Public License.
// ----------------------------------------------------------------------------------------------
// You must not remove this notice, or any other, from this software.
// ----------------------------------------------------------------------------------------------


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace MetaProgramming
{
    // ------------------------------------------------------------------------
    partial class Order : BaseViewModel
    {

        // --------------------------------------------------------------------
        // Properties
        // --------------------------------------------------------------------
        long _Id;
        public long Id
        {
            get { return _Id; }
            set
            {
                _Id = value;
                RaisePropertyChanged ("Id");
            }
        }
        // --------------------------------------------------------------------

        // --------------------------------------------------------------------
        DeliveryAddress _DeliverAddress;
        public DeliveryAddress DeliverAddress
        {
            get { return _DeliverAddress; }
            set
            {
                _DeliverAddress = value;
                RaisePropertyChanged ("DeliverAddress");
            }
        }
        // --------------------------------------------------------------------

        // --------------------------------------------------------------------
        OrderRow[] _Rows;
        public OrderRow[] Rows
        {
            get { return _Rows; }
            set
            {
                _Rows = value;
                RaisePropertyChanged ("Rows");
            }
        }
        // --------------------------------------------------------------------


    }
    // ------------------------------------------------------------------------

    // ------------------------------------------------------------------------
    partial class DeliveryAddress : BaseViewModel
    {

        // --------------------------------------------------------------------
        // Properties
        // --------------------------------------------------------------------
        long _Id;
        public long Id
        {
            get { return _Id; }
            set
            {
                _Id = value;
                RaisePropertyChanged ("Id");
            }
        }
        // --------------------------------------------------------------------

        // --------------------------------------------------------------------
        string _Name;
        public string Name
        {
            get { return _Name; }
            set
            {
                _Name = value;
                RaisePropertyChanged ("Name");
            }
        }
        // --------------------------------------------------------------------

        // --------------------------------------------------------------------
        string _Address;
        public string Address
        {
            get { return _Address; }
            set
            {
                _Address = value;
                RaisePropertyChanged ("Address");
            }
        }
        // --------------------------------------------------------------------

        // --------------------------------------------------------------------
        string _City;
        public string City
        {
            get { return _City; }
            set
            {
                _City = value;
                RaisePropertyChanged ("City");
            }
        }
        // --------------------------------------------------------------------

        // --------------------------------------------------------------------
        string _Zip;
        public string Zip
        {
            get { return _Zip; }
            set
            {
                _Zip = value;
                RaisePropertyChanged ("Zip");
            }
        }
        // --------------------------------------------------------------------

        // --------------------------------------------------------------------
        string _Country;
        public string Country
        {
            get { return _Country; }
            set
            {
                _Country = value;
                RaisePropertyChanged ("Country");
            }
        }
        // --------------------------------------------------------------------


    }
    // ------------------------------------------------------------------------

    // ------------------------------------------------------------------------
    partial class OrderRow : BaseViewModel
    {

        // --------------------------------------------------------------------
        // Properties
        // --------------------------------------------------------------------
        long _Id;
        public long Id
        {
            get { return _Id; }
            set
            {
                _Id = value;
                RaisePropertyChanged ("Id");
            }
        }
        // --------------------------------------------------------------------

        // --------------------------------------------------------------------
        string _Description;
        public string Description
        {
            get { return _Description; }
            set
            {
                _Description = value;
                RaisePropertyChanged ("Description");
            }
        }
        // --------------------------------------------------------------------

        // --------------------------------------------------------------------
        decimal _FullAmount;
        public decimal FullAmount
        {
            get { return _FullAmount; }
            set
            {
                _FullAmount = value;
                RaisePropertyChanged ("FullAmount");
            }
        }
        // --------------------------------------------------------------------

        // --------------------------------------------------------------------
        decimal _TaxAmount;
        public decimal TaxAmount
        {
            get { return _TaxAmount; }
            set
            {
                _TaxAmount = value;
                RaisePropertyChanged ("TaxAmount");
            }
        }
        // --------------------------------------------------------------------


    }
    // ------------------------------------------------------------------------

}

