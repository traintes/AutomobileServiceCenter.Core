using ASC.Models.Models;
using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ASC.Web.Areas.Promotions.Models
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Promotion, PromotionViewModel>();
            CreateMap<PromotionViewModel, Promotion>();
        }
    }
}
