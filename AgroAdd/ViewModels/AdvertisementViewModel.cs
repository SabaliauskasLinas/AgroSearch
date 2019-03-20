using AgroAdd.Models;
using AgroAdd.Models.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgroAdd.ViewModels
{
    public class AdvertisementViewModel: BaseViewModel
    {
        
        public AdvertisementViewModel(Advertisement model)
        {
            Model = model;
        }

        public Advertisement Model { get; set; }

        public string Name
        {
            get { return Model.Name; }
            set
            {
                Model.Name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
        public string Description
        {
            get { return Model.Description; }
            set
            {
                Model.Description = value;
                OnPropertyChanged(nameof(Description));
            }
        }
        public string ImageUrl
        {
            get { return Model.ImageUrl; }
            set
            {
                Model.ImageUrl = value;
                OnPropertyChanged(nameof(ImageUrl));
            }
        }
        public string PageUrl
        {
            get { return Model.PageUrl; }
            set
            {
                Model.PageUrl = value;
                OnPropertyChanged(nameof(PageUrl));
            }
        }
        public string Price
        {
            get { return Model.Price; }
            set
            {
                Model.Price = value;
                OnPropertyChanged(nameof(Price));
            }
        }


    }
}
