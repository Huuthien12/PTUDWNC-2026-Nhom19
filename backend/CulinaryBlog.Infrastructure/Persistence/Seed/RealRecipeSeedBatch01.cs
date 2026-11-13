using CulinaryBlog.Domain.Enums;

namespace CulinaryBlog.Infrastructure.Persistence.Seed;

// Batch 01: editorial seed data. Later batches append records; no random content belongs here.
public static class RealRecipeSeedBatch01
{
    public static readonly IReadOnlyList<RealRecipeSeed> Items =
    [
        Dish("Phở bò Hà Nội", "Món Việt", "Nước dùng trong, thơm quế hồi ăn cùng bánh phở và thịt bò tái.", 25, 180, 4, RecipeDifficulty.Medium,
            [I("Xương bò", 1, "kg"), I("Bánh phở tươi", 800, "g"), I("Thăn bò", 400, "g"), I("Hành tây", 1, "củ"), I("Gừng", 40, "g"), I("Hoa hồi", 3, "cánh"), I("Quế", 1, "thanh"), I("Hành lá", 50, "g"), I("Rau thơm", 100, "g")],
            [S("Nướng gia vị", "Nướng hành tây, gừng, quế và hoa hồi đến thơm.", 10), S("Ninh nước dùng", "Rửa xương, chần nhanh rồi ninh với gia vị trong 3 giờ; lọc trong.", 180), S("Chuẩn bị tô", "Trụng bánh phở và hành lá trong nước sôi, cho vào tô.", 3), S("Hoàn thiện", "Xếp thịt bò thái mỏng, chan nước dùng thật sôi và ăn cùng rau thơm.", 2)]),
        Dish("Bò bulgogi", "Món Á", "Thịt bò ướp ngọt mặn kiểu Hàn, áp chảo cùng hành tây và mè rang.", 20, 15, 4, RecipeDifficulty.Easy,
            [I("Thịt bò thăn", 500, "g"), I("Hành tây", 1, "củ"), I("Nước tương", 4, "thìa canh"), I("Đường nâu", 2, "thìa canh"), I("Dầu mè", 1, "thìa canh"), I("Tỏi", 3, "tép"), I("Mè rang", 1, "thìa canh"), I("Hành lá", 30, "g")],
            [S("Pha sốt ướp", "Trộn nước tương, đường nâu, dầu mè và tỏi băm.", 3), S("Ướp thịt", "Trộn thịt bò thái mỏng với sốt và để ngấm trong 15 phút.", 15), S("Áp chảo", "Làm nóng chảo, xào hành tây rồi cho bò vào đảo lửa lớn đến vừa chín.", 7), S("Dọn món", "Rắc hành lá và mè rang, dùng nóng với cơm.", 1)]),
        Dish("Mì Ý sốt bò bằm", "Món Âu", "Mì spaghetti phủ sốt cà chua bò bằm đậm vị.", 15, 35, 4, RecipeDifficulty.Easy,
            [I("Mì spaghetti", 350, "g"), I("Thịt bò bằm", 350, "g"), I("Cà chua hộp", 400, "g"), I("Hành tây", 1, "củ"), I("Tỏi", 3, "tép"), I("Dầu ô liu", 2, "thìa canh"), I("Lá oregano khô", 1, "thìa cà phê"), I("Phô mai parmesan", 30, "g")],
            [S("Luộc mì", "Luộc mì trong nước muối theo thời gian ghi trên gói, giữ lại ít nước luộc.", 10), S("Xào nền sốt", "Phi hành tây và tỏi với dầu ô liu đến trong.", 5), S("Nấu sốt", "Cho bò bằm vào xào tơi, thêm cà chua và oregano, đun liu riu đến sánh.", 20), S("Trộn và dọn", "Trộn mì với sốt, thêm nước luộc nếu cần và rắc parmesan.", 3)]),
        Dish("Đậu hũ sốt nấm", "Món Chay", "Đậu hũ áp vàng trong sốt nấm thanh ngọt.", 15, 20, 4, RecipeDifficulty.Easy,
            [I("Đậu hũ trắng", 4, "bìa"), I("Nấm đùi gà", 250, "g"), I("Nấm hương", 100, "g"), I("Nước tương", 2, "thìa canh"), I("Dầu hào chay", 1, "thìa canh"), I("Tỏi", 2, "tép"), I("Hành lá", 20, "g")],
            [S("Sơ chế", "Lau sạch nấm, cắt lát; để đậu hũ ráo rồi cắt miếng.", 5), S("Áp đậu", "Áp chảo đậu với ít dầu đến vàng hai mặt.", 8), S("Nấu sốt", "Xào tỏi và nấm, nêm nước tương cùng dầu hào chay, thêm 80 ml nước.", 5), S("Hoàn thiện", "Cho đậu vào áo sốt, rắc hành lá và dùng nóng.", 2)]),
        Dish("Sườn nướng sả mật ong", "Món Nướng", "Sườn heo nướng thơm sả, có vị ngọt nhẹ của mật ong.", 25, 35, 4, RecipeDifficulty.Medium,
            [I("Sườn non", 800, "g"), I("Sả", 4, "cây"), I("Mật ong", 2, "thìa canh"), I("Nước mắm", 2, "thìa canh"), I("Tỏi", 4, "tép"), I("Dầu ăn", 1, "thìa canh"), I("Tiêu", 0.5m, "thìa cà phê")],
            [S("Pha ướp", "Băm sả, tỏi rồi trộn cùng mật ong, nước mắm, dầu và tiêu.", 5), S("Ướp sườn", "Xoa hỗn hợp lên sườn, bọc kín và để lạnh ít nhất 1 giờ.", 60), S("Nướng", "Nướng ở 190°C, trở mặt và phết sốt ướp đến khi sườn chín vàng.", 30), S("Nghỉ thịt", "Để sườn nghỉ 5 phút trước khi chặt và dùng.", 5)]),
        Dish("Chả giò tôm thịt", "Món Chiên", "Chả giò giòn với nhân tôm, thịt và rau củ.", 30, 20, 4, RecipeDifficulty.Medium,
            [I("Bánh tráng cuốn", 20, "lá"), I("Thịt heo xay", 250, "g"), I("Tôm", 200, "g"), I("Miến", 50, "g"), I("Cà rốt", 1, "củ"), I("Nấm mèo", 30, "g"), I("Trứng", 1, "quả"), I("Dầu chiên", 500, "ml")],
            [S("Sơ chế nhân", "Ngâm miến và nấm mèo, để ráo rồi băm nhỏ cùng tôm.", 15), S("Trộn nhân", "Trộn thịt, tôm, rau củ, miến, nấm và trứng; nêm vừa ăn.", 5), S("Cuốn", "Đặt lượng nhân vừa phải lên bánh tráng, cuốn chặt hai đầu.", 10), S("Chiên", "Chiên lửa vừa đến vàng giòn, để ráo dầu trước khi dùng.", 12)]),
        Dish("Bò xào bông cải", "Món Xào", "Thịt bò mềm xào nhanh cùng bông cải xanh giòn ngọt.", 15, 12, 4, RecipeDifficulty.Easy,
            [I("Thịt bò thăn", 400, "g"), I("Bông cải xanh", 350, "g"), I("Tỏi", 3, "tép"), I("Dầu hào", 1, "thìa canh"), I("Nước tương", 1, "thìa canh"), I("Dầu ăn", 2, "thìa canh"), I("Tiêu", 0.5m, "thìa cà phê")],
            [S("Sơ chế", "Cắt bò mỏng, tách bông cải thành miếng vừa ăn và chần 1 phút.", 6), S("Xào bò", "Phi tỏi, xào bò lửa lớn đến tái rồi trút ra đĩa.", 3), S("Xào rau", "Xào bông cải với dầu hào và nước tương đến vừa chín.", 4), S("Hoàn thiện", "Cho bò trở lại chảo, đảo nhanh, rắc tiêu và tắt bếp.", 2)]),
        Dish("Cá hấp gừng hành", "Món Hấp", "Cá hấp giữ vị ngọt tự nhiên, thơm gừng và hành lá.", 20, 25, 4, RecipeDifficulty.Easy,
            [I("Cá diêu hồng", 800, "g"), I("Gừng", 40, "g"), I("Hành lá", 50, "g"), I("Nước tương", 3, "thìa canh"), I("Dầu mè", 1, "thìa canh"), I("Ớt", 1, "quả"), I("Rượu trắng", 1, "thìa canh")],
            [S("Làm sạch cá", "Làm sạch cá, khứa vài đường và xát rượu trắng để khử mùi.", 8), S("Xếp gia vị", "Nhét gừng thái sợi và hành vào bụng cá, rưới nước tương.", 5), S("Hấp cá", "Hấp cách thủy đến khi thịt cá trắng đục và dễ tách xương.", 20), S("Dọn món", "Rưới dầu mè nóng, thêm hành lá và ớt trước khi dùng.", 2)]),
        Dish("Thịt kho trứng", "Món Kho", "Thịt ba chỉ kho nước dừa cùng trứng cho bữa cơm nhà.", 20, 70, 4, RecipeDifficulty.Easy,
            [I("Thịt ba chỉ", 700, "g"), I("Trứng vịt", 6, "quả"), I("Nước dừa", 700, "ml"), I("Nước mắm", 3, "thìa canh"), I("Đường", 2, "thìa canh"), I("Hành tím", 3, "củ"), I("Tiêu", 0.5m, "thìa cà phê")],
            [S("Luộc trứng", "Luộc chín trứng, bóc vỏ và để riêng.", 10), S("Ướp thịt", "Cắt thịt miếng vuông, ướp nước mắm, đường, hành tím và tiêu.", 20), S("Kho", "Thắng chút đường, cho thịt vào săn, thêm nước dừa và kho lửa nhỏ.", 50), S("Thêm trứng", "Cho trứng vào kho thêm 20 phút đến khi thấm màu.", 20)]),
        Dish("Canh chua cá", "Món Canh", "Canh cá vị chua thanh của me, thơm dứa và rau ngổ.", 20, 25, 4, RecipeDifficulty.Medium,
            [I("Cá basa", 500, "g"), I("Dứa", 0.5m, "quả"), I("Cà chua", 2, "quả"), I("Giá đỗ", 150, "g"), I("Me chín", 30, "g"), I("Đậu bắp", 100, "g"), I("Rau ngổ", 30, "g"), I("Nước mắm", 2, "thìa canh")],
            [S("Nấu nước me", "Dằm me với 800 ml nước nóng, lọc lấy nước chua.", 5), S("Nấu rau", "Đun nước me, cho dứa, cà chua và đậu bắp vào nấu sôi.", 8), S("Nấu cá", "Thả cá vào, hạ lửa để cá chín mà không vỡ.", 8), S("Hoàn thiện", "Nêm nước mắm, thêm giá và rau ngổ, tắt bếp ngay.", 2)]),
        Dish("Súp bí đỏ", "Món Súp", "Súp bí đỏ mịn, béo nhẹ, phù hợp bữa nhẹ.", 15, 30, 4, RecipeDifficulty.Easy,
            [I("Bí đỏ", 700, "g"), I("Khoai tây", 1, "củ"), I("Hành tây", 0.5m, "củ"), I("Nước dùng rau củ", 800, "ml"), I("Kem tươi", 100, "ml"), I("Bơ lạt", 20, "g"), I("Hạt bí", 20, "g")],
            [S("Sơ chế", "Gọt bí và khoai, cắt khối nhỏ; băm hành tây.", 8), S("Xào thơm", "Đun bơ, xào hành tây đến trong rồi cho bí và khoai vào đảo.", 5), S("Ninh", "Thêm nước dùng, nấu đến khi rau củ mềm hoàn toàn.", 20), S("Xay mịn", "Xay nhuyễn, thêm kem tươi, nêm vừa và rắc hạt bí.", 5)]),
        Dish("Salad gà nướng", "Món Salad", "Rau xanh tươi cùng ức gà nướng và sốt chanh mật ong.", 20, 15, 4, RecipeDifficulty.Easy,
            [I("Ức gà", 400, "g"), I("Xà lách", 200, "g"), I("Cà chua bi", 200, "g"), I("Dưa leo", 1, "quả"), I("Bắp ngọt", 150, "g"), I("Chanh", 1, "quả"), I("Mật ong", 1, "thìa canh"), I("Dầu ô liu", 2, "thìa canh")],
            [S("Ướp gà", "Ướp ức gà với muối, tiêu và một thìa dầu ô liu.", 10), S("Nướng gà", "Áp chảo hoặc nướng gà đến chín, để nghỉ rồi thái lát.", 12), S("Pha sốt", "Khuấy nước cốt chanh, mật ong và dầu ô liu đến quyện.", 3), S("Trộn salad", "Trộn rau, bắp, cà chua, dưa leo với sốt, xếp gà lên trên.", 3)]),
        Dish("Bánh mì trứng ốp la", "Món Ăn Sáng", "Bữa sáng nhanh với trứng ốp la, pate và rau tươi.", 10, 10, 2, RecipeDifficulty.Easy,
            [I("Bánh mì", 2, "ổ"), I("Trứng gà", 2, "quả"), I("Pate", 60, "g"), I("Dưa leo", 0.5m, "quả"), I("Cà chua", 1, "quả"), I("Rau mùi", 20, "g"), I("Bơ", 10, "g"), I("Tiêu", 0.25m, "thìa cà phê")],
            [S("Sơ chế", "Rửa rau, thái dưa leo và cà chua; rạch bánh mì.", 5), S("Nướng bánh", "Làm nóng bánh mì cho vỏ giòn, quét pate bên trong.", 3), S("Ốp trứng", "Đun bơ, đập trứng và chiên đến độ chín mong muốn.", 4), S("Lắp bánh", "Cho rau, trứng, tiêu vào bánh và dùng ngay.", 2)]),
        Dish("Cơm gà Hội An", "Món Ăn Trưa", "Cơm nghệ vàng ăn cùng gà xé, rau răm và hành phi.", 25, 50, 4, RecipeDifficulty.Medium,
            [I("Gà ta", 1, "kg"), I("Gạo thơm", 400, "g"), I("Nghệ tươi", 20, "g"), I("Hành tây", 1, "củ"), I("Rau răm", 30, "g"), I("Chanh", 1, "quả"), I("Nước mắm", 2, "thìa canh"), I("Hành phi", 30, "g")],
            [S("Luộc gà", "Luộc gà với gừng và hành đến chín, giữ lại nước luộc.", 30), S("Nấu cơm", "Xào gạo với nghệ giã, nấu bằng nước luộc gà đến hạt cơm tơi.", 25), S("Trộn gà", "Xé gà, trộn hành tây, rau răm, chanh và nước mắm.", 8), S("Dọn cơm", "Xới cơm vàng, xếp gà trộn và rắc hành phi.", 2)]),
        Dish("Cá hồi áp chảo", "Món Ăn Tối", "Cá hồi da giòn dùng với măng tây và sốt chanh bơ.", 15, 18, 4, RecipeDifficulty.Medium,
            [I("Phi lê cá hồi", 600, "g"), I("Măng tây", 300, "g"), I("Bơ lạt", 30, "g"), I("Chanh", 1, "quả"), I("Tỏi", 2, "tép"), I("Dầu ô liu", 1, "thìa canh"), I("Muối", 0.5m, "thìa cà phê")],
            [S("Làm khô cá", "Thấm khô cá hồi, nêm muối và tiêu ở cả hai mặt.", 5), S("Áp cá", "Áp mặt da xuống chảo nóng đến giòn, trở mặt và nấu đến vừa chín.", 10), S("Xào măng tây", "Xào măng tây với tỏi và dầu ô liu đến xanh giòn.", 5), S("Làm sốt", "Đun chảy bơ, thêm nước cốt chanh rồi rưới lên cá.", 3)]),
        Dish("Bắp xào bơ", "Món Ăn Vặt", "Bắp ngọt xào bơ cùng tôm khô và hành lá.", 10, 12, 4, RecipeDifficulty.Easy,
            [I("Bắp ngọt", 500, "g"), I("Tôm khô", 50, "g"), I("Bơ lạt", 25, "g"), I("Hành lá", 30, "g"), I("Hành tím", 2, "củ"), I("Đường", 1, "thìa cà phê"), I("Nước mắm", 1, "thìa cà phê")],
            [S("Ngâm tôm", "Ngâm tôm khô trong nước ấm 5 phút rồi để ráo.", 5), S("Phi hành", "Đun bơ, phi hành tím thơm rồi cho tôm khô vào đảo.", 3), S("Xào bắp", "Cho bắp, đường và nước mắm vào xào đến khi bắp nóng đều.", 5), S("Hoàn thiện", "Thêm hành lá, đảo nhanh và dùng nóng.", 1)]),
        Dish("Bánh bông lan vanilla", "Bánh Ngọt", "Bánh bông lan mềm nhẹ, thơm vanilla.", 20, 35, 8, RecipeDifficulty.Medium,
            [I("Trứng gà", 4, "quả"), I("Bột mì đa dụng", 120, "g"), I("Đường", 120, "g"), I("Bơ lạt", 40, "g"), I("Sữa tươi", 60, "ml"), I("Tinh chất vanilla", 1, "thìa cà phê"), I("Bột nở", 1, "thìa cà phê")],
            [S("Chuẩn bị khuôn", "Làm nóng lò 170°C, lót giấy nến đáy khuôn.", 10), S("Đánh trứng", "Đánh trứng với đường đến bông nhạt màu.", 8), S("Trộn bột", "Rây bột và bột nở vào hỗn hợp, fold cùng bơ sữa và vanilla.", 5), S("Nướng bánh", "Đổ khuôn, nướng đến khi xiên tăm rút ra khô.", 30)]),
        Dish("Bánh flan caramel", "Tráng Miệng", "Flan mềm mịn với lớp caramel đắng nhẹ.", 20, 45, 6, RecipeDifficulty.Medium,
            [I("Trứng gà", 4, "quả"), I("Sữa tươi", 500, "ml"), I("Sữa đặc", 120, "g"), I("Đường", 80, "g"), I("Vanilla", 1, "thìa cà phê"), I("Nước", 30, "ml")],
            [S("Làm caramel", "Đun đường với nước đến màu hổ phách, chia vào đáy khuôn.", 8), S("Pha flan", "Hâm ấm sữa với sữa đặc, khuấy cùng trứng và vanilla, không đánh bọt.", 8), S("Lọc và rót", "Lọc hỗn hợp qua rây rồi rót vào khuôn caramel.", 4), S("Hấp hoặc nướng", "Hấp lửa nhỏ hoặc nướng cách thủy đến khi flan vừa đông.", 35)]),
        Dish("Trà đào cam sả", "Đồ Uống", "Trà đen mát lạnh với đào, cam và hương sả.", 15, 8, 2, RecipeDifficulty.Easy,
            [I("Trà đen", 2, "túi"), I("Đào ngâm", 200, "g"), I("Nước đào ngâm", 100, "ml"), I("Cam", 1, "quả"), I("Sả", 2, "cây"), I("Đường", 20, "g"), I("Đá viên", 300, "g")],
            [S("Nấu sả", "Đập dập sả, đun với 200 ml nước và đường rồi để nguội.", 5), S("Ủ trà", "Ủ trà đen với 250 ml nước nóng trong 3 phút, để nguội.", 3), S("Pha", "Trộn trà, nước sả, nước đào và nước cam theo khẩu vị.", 2), S("Dùng lạnh", "Cho đá và đào vào ly, rót trà rồi trang trí lát cam.", 1)]),
        Dish("Tôm rang me", "Hải Sản", "Tôm rang sốt me chua ngọt, thơm tỏi và hành lá.", 15, 20, 4, RecipeDifficulty.Easy,
            [I("Tôm sú", 600, "g"), I("Me chín", 50, "g"), I("Tỏi", 4, "tép"), I("Đường", 2, "thìa canh"), I("Nước mắm", 1, "thìa canh"), I("Hành lá", 30, "g"), I("Dầu ăn", 2, "thìa canh")],
            [S("Làm sốt me", "Dằm me với 120 ml nước nóng, lọc lấy nước cốt.", 5), S("Sơ chế tôm", "Rửa tôm, cắt râu và để ráo; có thể giữ vỏ để tôm ngọt.", 5), S("Rang tôm", "Phi tỏi, cho tôm vào rang đến khi chuyển đỏ.", 6), S("Áo sốt", "Thêm nước me, đường và nước mắm, rim đến khi sốt bám quanh tôm; rắc hành lá.", 5)])
    ];

    private static RealRecipeSeed Dish(string title, string category, string description, int prep, int cook, int servings, RecipeDifficulty difficulty, IngredientSeed[] ingredients, StepSeed[] steps) => new(title, category, description, prep, cook, servings, difficulty, ingredients, steps);
    private static IngredientSeed I(string name, decimal quantity, string unit, string? notes = null) => new(name, quantity, unit, notes);
    private static StepSeed S(string title, string description, int? minutes) => new(title, description, minutes);
}

public sealed record RealRecipeSeed(string Title, string CategoryName, string Description, int PrepTime, int CookTime, int Servings, RecipeDifficulty Difficulty, IReadOnlyList<IngredientSeed> Ingredients, IReadOnlyList<StepSeed> Steps);
public sealed record IngredientSeed(string Name, decimal Quantity, string Unit, string? Notes);
public sealed record StepSeed(string Title, string Description, int? TimerMinutes);
