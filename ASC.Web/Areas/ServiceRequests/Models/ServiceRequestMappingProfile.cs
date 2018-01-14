using ASC.Models.Models;
using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ASC.Web.Areas.ServiceRequests.Models
{
    public class ServiceRequestMappingProfile : Profile
    {
        public ServiceRequestMappingProfile()
        {
            CreateMap<ServiceRequest, NewServiceRequestViewModel>();
            CreateMap<NewServiceRequestViewModel, ServiceRequest>();
            CreateMap<ServiceRequest, UpdateServiceRequestViewModel>();
            CreateMap<UpdateServiceRequestViewModel, ServiceRequest>();
        }
    }
}
