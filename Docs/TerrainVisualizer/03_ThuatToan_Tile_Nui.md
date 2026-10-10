# 03 — Thuật toán chọn tile, ground, cliff và núi

## 1. Surface và top mask

Thứ tự corner là `00,10,11,01`; trọng số bit là `1,2,4,8`. Builder lấy bốn độ cao từ `TerrainVisualSurface.CellHeight`, tìm `bottom=min(h)`, rồi đặt bit k nếu `(h[k]-bottom)/rise > 0.5`. Nếu bất kỳ corner cao hơn bottom quá 1.001 tier thì reject.

Mask 0 chọn ground variation; mask khác chọn một trong 16 top slots. Mask của top và mask endpoint wall là hai hệ ý nghĩa khác nhau. Không dùng tile ID trong `map.draws` làm index trực tiếp của wall sprite.

Trong shared-corner mode, với tọa độ trong ô `(u,v)`, `B=(heightLevel-minimum)*rise` và các bit cao `nw,ne,se,sw`:

```text
v <= u: H = B + rise * [nw + (ne-nw)u + (se-ne)v]
v >  u: H = B + rise * [nw + (se-sw)u + (sw-nw)v]
```

Hai miền là hai tam giác chia theo đường 00–11, khớp cùng đường chéo. Saddle hoặc corner không chung nhất quán là trách nhiệm kiểm tra đầu vào/generator; renderer không âm thầm đổi topology để sửa ảnh.

## 2. Biến thể mặt đất độc lập gameplay

`GetFlatGroundSprite(x,z)` dùng hash tọa độ với `groundVisualSeed`, không lấy RNG của terrain/resource planner. Perlin vùng lớn cung cấp xác suất chọn lush/sparse bank; hash chọn chi tiết từng ô. Vì đây là lựa chọn xác suất theo ô, không được gọi nó là blend vật liệu liên tục.

Khi `groundPatternColumns > 0`, số ảnh phải bằng `2*columns²`. Bộ hiện tại dùng columns=4, tức hai bank 16 tile. Index trong bank lấy modulo tọa độ cộng offset seed; modulo đã xử lý tọa độ âm. Nếu cấu hình rỗng, dùng topFlatMask0; nếu ảnh variation null, fallback slot mặt ngang.

Tính liên tục trong mỗi bank phụ thuộc artwork đã author đúng mép. Trộn lush/sparse vẫn cần mép tương thích giữa bank. Không có Blendomatic đầy đủ, mask transition vật liệu hay shader blend được triển khai ở đây.

## 3. Wall hướng camera

Chỉ dựng cạnh −Z (00→10) và −X (00→01). Với một cạnh, độ cao cạnh hiện tại là `hiA,hiB`, mặt hàng xóm cung cấp `oa,ob`; `loA=min(hiA,oa)`, `loB=min(hiB,ob)`. Hàng xóm cao hơn có thể che cạnh nên không dựng phần âm.

Khoảng từ mức thấp đến cao được chia segment từng tier. Trong mỗi segment, bốn endpoint `lowA,lowB,highB,highA` được clamp về [0,1] rồi lượng tử hóa thành bit. Chỉ mask **4,8,C,D,E** có diện tích hiển thị ở mỗi side; tổng 10 PNG. Các mask còn lại không cần ảnh alpha rỗng. Cliff cao hai tầng là hai segment, không phải một ảnh kéo dãn.

## 4. Núi: BFS đa nguồn trên đỉnh chung

Input là `isMountain` có sẵn. Trường có kích thước `(width+1)*(height+1)`, không phải một chiều cao độc lập cho mỗi ô.

```text
for each shared vertex v:
    if any of its four incident cells is not mountain, or is outside map:
        distance[v] = 0; enqueue(v)
    else:
        distance[v] = infinity
while queue not empty:
    v = dequeue()
    for each of v's eight neighbors:
        if distance[n] > distance[v] + 1:
            distance[n] = distance[v] + 1; enqueue(n)
level[v] = min(maxTiers, distance[v])
```

Khoảng cách là số bước trên graph 8-neighbor có trọng số đều, không phải Euclidean distance. Cạnh và đường chéo đều tốn một bước. Suy luận: hai đỉnh hàng xóm chênh nhiều nhất một level; clamp cùng maxTiers giữ tính chất này. Đỉnh tiếp xúc ô không phải mountain có level=0, nên không nở footprint núi vào ô walkable.

Mỗi mountain cell lấy bốn level chung, tính low và mask các góc cao hơn low, chọn `mountainCaps[mask]`, đặt base tại `bottom+low*rise`. Chỉ chọn field khi array có đủ 16 cap. Mỗi cap có span một tier. Nó là nền đá ghép của cụm, không phải 16 boulder rời hay dựng núi vật lý.

Độ phức tạp suy luận O(V+E), với V là số đỉnh và E tối đa khoảng 8V; bộ nhớ O(V). Clamp diễn ra sau BFS toàn trường; hiện chưa có early exit khi đã đạt maxTiers. `Range(1,6)` là giới hạn Inspector, không phải validator cho mọi caller tự truyền tham số.

Theme không có cap vẫn có thể dùng `mountainArtwork` một ảnh/ô. Đây là fallback art của renderer hiện tại. Người dùng đã yêu cầu bỏ **thuật toán offset cũ và lab chạy lại nó**, không yêu cầu xóa mọi hình thức tương thích theme hoặc input MapGen.

## 5. Decor ngoài biên núi

Hash density được lọc trước phép quét lân cận. Ô phải walkable, không mountain/cliff/ramp; có núi trong lân cận 3×3; không có RampId trong 7×7; không nằm cách spawn dưới 32 ô theo khoảng cách Euclidean bình phương. Artwork nhỏ được chọn bằng hash modulo số texture.

Đây là accent không collider; nó không spawn resource, không đổi cost và không thay resource exclusion. Density chỉ là sampling gate, không bảo đảm quota trang trí chính xác.

## 6. Cách bác bỏ một thay đổi

Hash gameplay trước/sau build phải bằng nhau. Với field núi: mọi cạnh 8-neighbor chênh ≤1, mọi đỉnh chạm ô ngoài mountain phải bằng 0. Với ground: đổi seed visual chỉ đổi art selection. Với wall: không pack 22 slot alpha0 đã loại. Với decor: fixture ramp/spawn phải chặn accent đúng phạm vi.

Các tiêu chí trên kết hợp structural test và screenshot; chỉ kiểm tra mảng hợp lệ chưa chứng minh artwork nối đẹp. Xem [lab](06_Lab_BangChung.md).
