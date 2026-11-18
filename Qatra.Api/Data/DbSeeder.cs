using Microsoft.EntityFrameworkCore;
using Qatra.Api.Models;

namespace Qatra.Api.Data;

public static class DbSeeder
{
    public static async Task SeedNeighborhoodsAsync(AppDbContext db)
    {
        // لا نُدخل شيئًا إن كانت الأحياء موجودة، فلا يتكرر الإدخال
        if (await db.Neighborhoods.AnyAsync())
            return;

        db.Neighborhoods.AddRange(
            new Neighborhood { NameAr = "الديس - الهجرة والجوازات", Lat = 14.5485, Lng = 49.1312, Population = 12000, PriorCycleDays = 2.0, PriorCycleSd = 0.5 },
            new Neighborhood { NameAr = "فوه - المتضررين (وحدة الفاروق)", Lat = 14.4892, Lng = 49.0485, Population = 18000, PriorCycleDays = 2.5, PriorCycleSd = 1.5 },
            new Neighborhood { NameAr = "الشرج - باعبود", Lat = 14.5361, Lng = 49.1195, Population = 15000, PriorCycleDays = 2.0, PriorCycleSd = 0.6 },
            new Neighborhood { NameAr = "المكلا - حي السلام", Lat = 14.5320, Lng = 49.1280, Population = 14000, PriorCycleDays = 2.0, PriorCycleSd = 0.4 },
            new Neighborhood { NameAr = "الديس - شعب البادية والمخفي", Lat = 14.5540, Lng = 49.1360, Population = 16000, PriorCycleDays = 2.0, PriorCycleSd = 1.8 },
            new Neighborhood { NameAr = "المكلا - أربعين شقة", Lat = 14.5410, Lng = 49.1150, Population = 11000, PriorCycleDays = 2.5, PriorCycleSd = 0.7 },
            new Neighborhood { NameAr = "الديس - باناعمة", Lat = 14.5460, Lng = 49.1290, Population = 9000, PriorCycleDays = 2.0, PriorCycleSd = 0.5 },
            new Neighborhood { NameAr = "حي أكتوبر - الشيشان", Lat = 14.5435, Lng = 49.1240, Population = 13000, PriorCycleDays = 2.0, PriorCycleSd = 0.7 },
            new Neighborhood { NameAr = "فوه - ربوة المهندسين", Lat = 14.4980, Lng = 49.0350, Population = 8000, PriorCycleDays = 30.0, PriorCycleSd = 10.0 },
            new Neighborhood { NameAr = "الشحر - حارة ليبيا", Lat = 14.7610, Lng = 49.6080, Population = 10000, PriorCycleDays = 2.5, PriorCycleSd = 0.8 }
        );

        await db.SaveChangesAsync();
    }
}