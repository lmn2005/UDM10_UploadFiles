import fs from "node:fs/promises";
import path from "node:path";
import sharp from "sharp";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const root = "/Users/tanhiep/Desktop/UDM10_UploadFiles";
const outputDir = path.join(root, "outputs/01a0d91e-3830-73e1-a279-82a9abbbc916");
const evidenceDir = path.join(root, "tmp/xlsx_build/evidence");
await fs.mkdir(outputDir, { recursive: true });
await fs.mkdir(evidenceDir, { recursive: true });

const people = [
  { name: "Nguyễn Tấn Hiệp", short: "Hiệp", prefix: "H", tab: "Nguyễn Tấn Hiệp", color: "#DCE6F1" },
  { name: "Phạm Anh Tuấn", short: "Tuấn", prefix: "T", tab: "Phạm Anh Tuấn", color: "#E2F0D9" },
  { name: "Huỳnh Anh Kiệt", short: "Kiệt", prefix: "K", tab: "Huỳnh Anh Kiệt", color: "#FCE4D6" },
  { name: "Võ Nhật Linh", short: "Linh", prefix: "L", tab: "Võ Nhật Linh", color: "#FFF2CC" },
  { name: "Lê Văn Nhựt", short: "Nhựt", prefix: "N", tab: "Lê Văn Nhựt", color: "#E4DFEC" },
];

const cases = [];
function add(owner, week, feature, items, kind = "Theo phân công gốc") {
  const p = people.find(x => x.short === owner);
  for (const [sourceId, title] of items) {
    const seq = cases.filter(x => x.owner === p.name).length + 1;
    const id = `TC_${p.prefix}_${String(seq).padStart(3, "0")}`;
    const detail = makeDetail(title, feature, week);
    cases.push({ id, sourceId, owner: p.name, week, feature, title, kind, ...detail });
  }
}

function makeDetail(title, feature, week) {
  const t = title.toLowerCase();
  let description = `Kiểm tra ${title.charAt(0).toLowerCase()}${title.slice(1)} trong chức năng ${feature}.`;
  let scenario = `Khởi động Server và Client từ bản build dùng cho Tuần ${week}. Chuẩn bị dữ liệu phù hợp, thực hiện tình huống “${title}”, theo dõi giao diện và lưu log liên quan.`;
  let expected = `Hệ thống xử lý đúng tình huống “${title}”; Client và Server không treo, không phát sinh lỗi ngoài dự kiến.`;

  if (feature.includes("Client WPF") || feature.includes("Client GUI") || feature.includes("Giao diện")) {
    if (t.includes("mở cửa sổ")) {
      scenario = "Chạy Client trên Windows, nhập địa chỉ IP Server và port hợp lệ rồi nhấn kết nối. Lặp lại với IP rỗng, port không phải số và port ngoài phạm vi.";
      expected = "Cửa sổ mở không lỗi; dữ liệu hợp lệ được nhận; dữ liệu thiếu/sai được báo rõ, không làm ứng dụng đóng đột ngột.";
    } else if (t.includes("chọn file bằng")) {
      scenario = "Nhấn nút chọn file, chọn một txt, một ảnh và một PDF. Sau đó đóng hộp thoại bằng Cancel.";
      expected = "File được chọn thêm đúng một lần; Cancel không thêm dữ liệu; tên, đường dẫn và kích thước khớp file trên đĩa.";
    } else if (t.includes("kéo thả file vào")) {
      scenario = "Kéo một file từ Explorer vào vùng nhận file; thử lại bằng cách thả lên ngoài vùng nhận và kéo một thư mục.";
      expected = "Chỉ file hợp lệ thả trong vùng nhận được thêm; thao tác ngoài vùng hoặc thư mục không làm lỗi giao diện.";
    } else if (t.includes("tên, đường dẫn và kích thước")) {
      scenario = "Chọn file có tên tiếng Việt, dấu cách và kích thước biết trước. So sánh các trường hiển thị với Properties của file.";
      expected = "Tên và đường dẫn không bị cắt sai; kích thước hiển thị đúng; dữ liệu Unicode được giữ nguyên.";
    } else if (t.includes("mỗi file một dòng")) {
      scenario = "Chọn 5 file khác nhau cùng lúc từ hộp thoại Open. Đối chiếu số dòng với số file đã chọn.";
      expected = "Có đúng 5 dòng, mỗi dòng đại diện một file với tên và kích thước tương ứng; không gộp nhầm các file.";
    } else if (t.includes("kéo thả nhiều file")) {
      scenario = "Chọn nhiều file trong Explorer rồi kéo cùng lúc vào vùng nhận file; lặp lại với danh sách có một file trùng đường dẫn đã thêm.";
      expected = "Các file hợp lệ cùng được thêm; file đã có không bị tạo dòng trùng; giao diện vẫn sử dụng được.";
    } else if (t.includes("waiting, uploading")) {
      scenario = "Thêm 5 file và chạy upload với giới hạn 3 phiên. Quan sát từng dòng trong lúc chờ, truyền xong và khi cố ý làm một file lỗi.";
      expected = "Từng dòng chuyển đúng Waiting, Uploading, Completed hoặc Error theo tiến trình thật; không áp trạng thái của file này cho file khác.";
    } else if (t.includes("progress và tốc độ")) {
      scenario = "Upload đồng thời một file nhỏ và một file lớn. Ghi phần trăm/tốc độ của từng dòng ở nhiều thời điểm.";
      expected = "Progress và tốc độ thay đổi theo dữ liệu của đúng file, không giảm ngoài quy luật và kết thúc ở 100% khi Completed.";
    } else if (t.includes("không thêm hai lần")) {
      scenario = "Thêm cùng một đường dẫn hai lần bằng cách chọn file rồi kéo thả lại. Sau đó chọn file cùng tên ở thư mục khác.";
      expected = "Đường dẫn đã có không sinh bản ghi trùng; file cùng tên nhưng khác đường dẫn được xử lý theo quy tắc của ứng dụng.";
    } else if (t.includes("vẫn phản hồi") || t.includes("không treo") || t.includes("cập nhật an toàn")) {
      scenario = "Bắt đầu 3 upload đồng thời, tiếp tục cuộn danh sách, kéo cửa sổ và thay đổi kích thước trong lúc progress liên tục cập nhật.";
      expected = "Cửa sổ tiếp tục nhận thao tác; các dòng cập nhật đúng trên UI thread; không có lỗi cross-thread hoặc cửa sổ Not Responding.";
    } else if (t.includes("hủy một file")) {
      scenario = "Chạy đồng thời 3 file, bấm Cancel ở một dòng Uploading. Tiếp tục quan sát hai dòng còn lại và một file trong queue.";
      expected = "Chỉ dòng được chọn thành Cancelled; dòng khác không đổi trạng thái; slot trống được cấp cho file chờ.";
    } else if (t.includes("thử lại file")) {
      scenario = "Tạo một dòng Error bằng cách ngắt kết nối, khôi phục Server rồi bấm Retry trên đúng dòng đó.";
      expected = "Chỉ file Error/Cancelled được chạy lại; progress bắt đầu theo lượt mới và dòng kết thúc Completed hoặc hiển thị lỗi mới.";
    } else if (t.includes("nút cancel và retry") || t.includes("nút hủy tất cả") || t.includes("nút thử lại tất cả") || t.includes("nút xóa mục")) {
      scenario = "Tạo danh sách có các file Waiting, Uploading, Error, Cancelled và Completed; kiểm tra nút liên quan khi trạng thái xuất hiện rồi biến mất.";
      expected = "Mỗi nút chỉ bật khi có loại mục phù hợp; nút không phù hợp bị khóa/ẩn và không gửi thao tác sai trạng thái.";
    } else if (t.includes("progress file khác")) {
      scenario = "Upload đồng thời ít nhất 3 file; hủy một file khi các file kia đang nhận dữ liệu. So sánh progress trước và sau thao tác.";
      expected = "Progress hai file còn lại tiếp tục tăng bình thường, không reset hoặc đổi chéo dòng.";
    } else if (t.includes("hiển thị lỗi riêng")) {
      scenario = "Làm một file lỗi do sai port hoặc ngắt kết nối trong khi một file khác upload thành công.";
      expected = "Lỗi gắn vào đúng dòng với nội dung có thể hiểu; file thành công vẫn hiển thị Completed.";
    } else if (t.includes("connected, disconnected")) {
      scenario = "Mở Client khi Server đang bật, dừng Server trong lúc sử dụng, rồi khởi động Server lại và kết nối lại.";
      expected = "Nhãn Connected/Disconnected/Error phản ánh trạng thái thật và cập nhật sau mỗi lần kết nối.";
    } else if (t.includes("tên file dài")) {
      scenario = "Thêm file có tên dài, ký tự tiếng Việt và phần mở rộng; xem ở kích thước cửa sổ mặc định và cửa sổ thu nhỏ.";
      expected = "Tên không đè lên cột/nút; phần bị rút gọn vẫn có thể đọc bằng tooltip hoặc cách hiển thị được thiết kế.";
    } else if (t.includes("20 file")) {
      scenario = "Kéo thả liên tiếp 20 file khác nhau thành hai đợt, trong khi một số file đang upload.";
      expected = "Danh sách đủ 20 file, không thêm trùng; UI không treo và trạng thái hàng đợi tiếp tục cập nhật.";
    } else if (t.includes("tốc độ hiển thị")) {
      scenario = "Upload file đủ lớn để tốc độ dao động qua ngưỡng 1 MB/s. So sánh đơn vị, giá trị và quy đổi giữa KB/s, MB/s.";
      expected = "Đơn vị đổi đúng ngưỡng; giá trị không bị nhân/chia sai 1024; không hiện NaN hoặc Infinity khi tốc độ bằng 0.";
    } else if (t.includes("thống kê phiên")) {
      scenario = "Chạy phiên có file Completed, Error và Cancelled; đối chiếu số file/byte/thời gian hiển thị với kết quả thực tế.";
      expected = "Các tổng và tốc độ phiên khớp với những file thuộc phiên, không tính nhầm mục đã xóa.";
    } else if (t.includes("thông báo tiếng việt")) {
      scenario = "Chụp giao diện ở các trạng thái Connected, Uploading, Completed, Error và Cancelled; kiểm tra nội dung thông báo và font tiếng Việt.";
      expected = "Thông báo đúng ngữ cảnh, dấu tiếng Việt hiển thị đầy đủ; ảnh chụp thể hiện đúng chức năng và trạng thái thực tế.";
    } else if (t.includes("xóa lịch sử") || t.includes("xóa completed")) {
      scenario = "Có file Completed, Waiting và Uploading. Bấm xóa mục hoàn tất và kiểm tra danh sách cùng thư mục Server.";
      expected = "Chỉ dòng Completed bị xóa khỏi lịch sử UI; file trên Server vẫn còn; Waiting/Uploading không bị ảnh hưởng.";
    } else if (t.includes("savedfilename") || t.includes("tên gốc")) {
      scenario = "Upload hai file cùng tên report.pdf; quan sát tên gửi ban đầu và tên Server lưu trong phản hồi.";
      expected = "Giao diện phân biệt tên gốc với SavedFileName, ví dụ report.pdf và report_1.pdf; không tự suy diễn tên lưu.";
    } else if (t.includes("danh sách cập nhật")) {
      scenario = "Trong lúc hàng loạt upload, hủy và thử lại, thực hiện thêm/xóa mục được phép; theo dõi các event cập nhật collection.";
      expected = "Danh sách cập nhật đúng trên UI thread; không InvalidOperationException, cross-thread exception hoặc mất dòng ngoài ý muốn.";
    }
  } else if (t.includes("build") || t.includes("solution") || t.includes("repository")) {
    scenario = "Clone/mở repository trên máy sạch; restore package; build toàn bộ solution ở cấu hình Release; kiểm tra tham chiếu Client, Server và Shared.";
    expected = "Solution build thành công, không thiếu project/reference; Client và Server khởi động được từ cấu hình hiện có.";
  } else if (t.includes("readme")) {
    scenario = "Làm theo README trên máy chưa cấu hình dự án: build, đổi port theo hướng dẫn, chạy Server rồi Client và upload một file nhỏ.";
    expected = "Các lệnh và đường dẫn trong README dùng được; dự án chạy đúng mà không cần đoán thêm bước cấu hình.";
  } else if (t.includes("metadata") || t.includes("protocol") || t.includes("requestid") || t.includes("filename rỗng") || t.includes("filesize")) {
    scenario = `Dùng test client gửi request có trường dữ liệu đúng với ca “${title}”. Ghi lại JSON gửi đi, phản hồi và ErrorCode.`;
    expected = `Request hợp lệ được xử lý; request sai bị từ chối bằng ERROR/ErrorCode phù hợp. Server vẫn tiếp tục nhận kết nối mới.`;
  } else if (t.includes("message bị cắt") || t.includes("đọc đủ byte")) {
    scenario = "Chia phần header/metadata thành nhiều lần gửi nhỏ, dừng giữa chừng ở một lượt, sau đó gửi đủ ở lượt khác để kiểm tra hàm đọc dữ liệu.";
    expected = "Message đủ được ghép và đọc chính xác; message thiếu bị timeout hoặc trả lỗi có kiểm soát, không làm Server crash.";
  } else if (t.includes("upload 1 file") || t.includes("file nhỏ") || t.includes("upload file mới") || t.includes("gửi một file")) {
    scenario = "Dùng file txt khoảng 10 KB có tên tiếng Việt. Chọn file, bắt đầu upload, chờ trạng thái cuối rồi mở file tại thư mục Server.";
    expected = "File chuyển từ Waiting sang Uploading và Completed; kích thước/nội dung phía Server khớp file gốc.";
  } else if (t.includes("3 file") || t.includes("5 file") || t.includes("10 file") || t.includes("20 file") || t.includes("30 file") || t.includes("nhiều file")) {
    scenario = `Tạo tập file khác tên theo đúng số lượng của ca “${title}”, gồm file nhỏ và file 10–20 MB. Thêm tất cả vào hàng đợi rồi theo dõi số phiên đang chạy.`;
    expected = "Không quá 3 file ở trạng thái Uploading; file chờ tự chạy khi có slot; không mất file, không upload trùng và thống kê cuối khớp số lượng.";
  } else if (t.includes("cancel") || t.includes("hủy")) {
    scenario = "Upload file 100 MB để có đủ thời gian thao tác. Hủy khi file đang Uploading; đồng thời để ít nhất một file khác chạy và một file Waiting.";
    expected = "File bị hủy chuyển Cancelled, slot được giải phóng, file khác tiếp tục; Server không công nhận file thiếu và không còn file .part sau cleanup.";
  } else if (t.includes("retry") || t.includes("thử lại")) {
    scenario = "Tạo một lần upload Error/Cancelled, khôi phục kết nối rồi bấm Retry. Theo dõi queue, số connection và số bản file lưu trên Server.";
    expected = "Chỉ file Error/Cancelled được đưa lại vào queue; một phiên upload mới được tạo đúng một lần và kết thúc Completed.";
  } else if (t.includes("trùng") || t.includes("duplicate")) {
    scenario = "Upload ba file cùng tên report.pdf nhưng khác nội dung. Kiểm tra tên lưu, phản hồi SavedFileName và từng file trên Server.";
    expected = "Không ghi đè file cũ; tên lưu lần lượt theo quy tắc report.pdf, report_1.pdf, report_2.pdf và Client hiển thị đúng tên thực tế.";
  } else if (t.includes("hash") || t.includes("checksum") || t.includes("integrity") || t.includes("nội dung file")) {
    scenario = "Tính SHA-256 file gốc, upload qua mạng, tính lại tại Server; lặp lại với một file bị sửa/thiếu byte bằng test client.";
    expected = "File đầy đủ có hash trùng; file thiếu hoặc sai hash không được báo Completed và được ghi nhận lỗi rõ ràng.";
  } else if (t.includes("kích thước") || t.includes("số byte") || t.includes("file thiếu")) {
    scenario = "Upload file có kích thước biết trước; so sánh tổng byte Client gửi, Server nhận và size trên đĩa. Lặp lại bằng cách ngắt trước byte cuối.";
    expected = "Ba giá trị byte khớp khi thành công; file thiếu không được đổi từ .part thành file hoàn chỉnh.";
  } else if (t.includes("file lớn") || t.includes("ram") || t.includes("throughput") || t.includes("benchmark") || t.includes("cpu")) {
    scenario = "Upload file lớn theo mức tải ghi trong ca kiểm thử. Ghi thời gian, MB/s, RAM và CPU trước, trong và sau khi chạy; lặp lại tối thiểu hai lần.";
    expected = "Upload hoàn tất, file mở được; RAM không tăng theo toàn bộ kích thước file; số liệu thời gian, throughput, CPU/RAM được ghi đầy đủ.";
  } else if (t.includes("log")) {
    scenario = "Chạy lần lượt upload thành công, Cancel, Retry, Error, timeout và disconnect. Mở log Server và đối chiếu RequestId, IP, tên file, số byte, thời gian.";
    expected = "Mỗi vòng đời có log Start và kết quả tương ứng; thông tin đủ để truy vết, không lộ dữ liệu ngoài yêu cầu và không thiếu sự kiện chính.";
  } else if (t.includes("disconnect") || t.includes("mất kết nối") || t.includes("ngắt") || t.includes("timeout")) {
    scenario = "Đang upload file 100 MB thì đóng Client/rút kết nối hoặc dừng gửi dữ liệu quá thời gian timeout; sau đó kết nối lại bằng Client khác.";
    expected = "Phiên lỗi được đóng và cleanup; Server không dừng, tiếp tục accept Client mới; Client hiển thị Error/Disconnected phù hợp.";
  } else if (t.includes("server") && (t.includes("vẫn chạy") || t.includes("restart") || t.includes("shutdown") || t.includes("kết nối"))) {
    scenario = "Khởi động Server, tạo kết nối theo ca kiểm thử, quan sát port/process/socket; kết thúc hoặc khởi động lại đúng thời điểm yêu cầu.";
    expected = "Server xử lý vòng đời đúng, giải phóng socket/stream, không treo và có thể khởi động/nhận kết nối lại.";
  } else if (t.includes("gui") || t.includes("giao diện") || t.includes("progress") || t.includes("button") || t.includes("tốc độ") || t.includes("tên file dài")) {
    scenario = `Thực hiện ca “${title}” trên giao diện WPF khi có ít nhất ba file ở các trạng thái khác nhau. Theo dõi nút, progress, tốc độ và khả năng thao tác cửa sổ.`;
    expected = "Thông tin hiển thị đúng từng file; nút chỉ hoạt động ở trạng thái hợp lệ; UI thread không bị khóa, không giật hoặc cập nhật nhầm dòng.";
  } else if (t.includes("thống kê")) {
    scenario = "Chạy một phiên gồm Completed, Error và Cancelled; ghi số file/byte/thời gian thực tế, sau đó xóa mục Completed trên giao diện.";
    expected = "Tổng file, Completed, Error, Cancelled, byte, thời gian và tốc độ được tính lại đúng; file thật trên Server vẫn còn.";
  } else if (t.includes(".part") || t.includes("file tạm") || t.includes("cleanup")) {
    scenario = "Theo dõi thư mục Uploads trong khi upload, sau đó tạo Cancel, Error và disconnect. Kiểm tra file .part, file Completed và log cleanup.";
    expected = "File .part chỉ tồn tại khi đang upload; được xóa sau lỗi/hủy; file Completed không bị xóa nhầm.";
  } else if (t.includes("port") || t.includes("cấu hình")) {
    scenario = "Đổi port ở file cấu hình của cả Client và Server, thử đúng port rồi thử sai port/port đang bị chiếm.";
    expected = "Đúng port kết nối được; sai port trả thông báo rõ; không có giá trị hard-code làm Client và Server lệch cấu hình.";
  }
  return { description, scenario, expected };
}

// Tuần 1: ca kiểm thử được xây dựng từ nhiệm vụ và sản phẩm bàn giao.
add("Hiệp", 1, "Khởi tạo repository và solution", [["W1-H01", "Repository có đúng ba project và reference"], ["W1-H02", "Clean build solution ở cấu hình Release"], ["W1-H03", "Client và Server đọc được appsettings"], ["W1-H04", "Repository không chứa file build tạm"]]);
add("Tuấn", 1, "Shared protocol phiên bản 1", [["W1-T01", "Serialize metadata hợp lệ"], ["W1-T02", "Deserialize phản hồi READY"], ["W1-T03", "Deserialize phản hồi COMPLETED và ERROR"], ["W1-T04", "ProtocolReader đọc đủ byte khi dữ liệu chia nhỏ"]]);
add("Kiệt", 1, "Server TCP và xử lý kết nối", [["W1-K01", "Server mở đúng port cấu hình"], ["W1-K02", "Server nhận metadata hợp lệ"], ["W1-K03", "Server từ chối metadata sai"], ["W1-K04", "Một Client lỗi nhưng Server vẫn chạy"]]);
add("Linh", 1, "Server lưu file và log", [["W1-L01", "Upload file txt nhỏ"], ["W1-L02", "Upload file png và pdf"], ["W1-L03", "Upload file có tên tiếng Việt"], ["W1-L04", "So sánh kích thước và nội dung file Client Server"]]);

// Phần kiểm thử bổ sung được chia đều cho bốn thành viên.
add("Hiệp", 1, "Luồng truyền một file", [["BS-W1-01", "Gửi một file hợp lệ qua TCP"]], "Phần bổ sung đã phân chia");
add("Tuấn", 1, "Luồng truyền một file", [["BS-W1-02", "Client xử lý Server chưa bật hoặc sai port"]], "Phần bổ sung đã phân chia");
add("Kiệt", 1, "Luồng truyền một file", [["BS-W1-03", "Server trả READY và COMPLETED đúng thứ tự"]], "Phần bổ sung đã phân chia");
add("Linh", 1, "Luồng truyền một file", [["BS-W1-04", "File được gửi theo chunk không nạp toàn bộ vào RAM"]], "Phần bổ sung đã phân chia");

add("Hiệp", 2, "Điều phối upload nhiều file phía Client", [["H01", "Upload 1 file"], ["H02", "Upload đúng 3 file cùng lúc"], ["H03", "Chọn 5 file, 3 chạy và 2 chờ"], ["H04", "Một file lỗi nhưng các file khác vẫn tiếp tục"]]);
add("Tuấn", 2, "Protocol upload v2", [["T01", "Metadata hợp lệ"], ["T02", "FileName rỗng"], ["T03", "FileSize không hợp lệ"], ["T04", "Server trả ERROR và Client đọc đúng lỗi"]]);
add("Kiệt", 2, "Server nhận nhiều kết nối", [["K01", "Một Client kết nối"], ["K02", "Ba Client kết nối đồng thời"], ["K03", "Một Client ngắt giữa chừng"], ["K04", "Client lỗi nhưng Server vẫn nhận Client mới"]]);
add("Linh", 2, "Truyền file theo chunk", [["L01", "File nhỏ"], ["L02", "File lớn"], ["L03", "File không chia hết kích thước chunk"], ["L04", "So sánh kích thước file Client Server"], ["L05", "Kiểm tra nội dung file sau upload"]]);
add("Hiệp", 2, "Storage và kiểm thử tích hợp", [["BS-W2-01", "Upload file mới"], ["BS-W2-02", "Upload 5 file liên tiếp"]], "Phần bổ sung đã phân chia");
add("Tuấn", 2, "Storage và kiểm thử tích hợp", [["BS-W2-03", "Nhiều Client upload cùng lúc"], ["BS-W2-04", "Kiểm tra log upload"]], "Phần bổ sung đã phân chia");
add("Kiệt", 2, "Storage và kiểm thử tích hợp", [["BS-W2-05", "Upload bị ngắt và file tạm được xử lý"]], "Phần bổ sung đã phân chia");
add("Linh", 2, "Storage và kiểm thử tích hợp", [["BS-W2-06", "Upload file trùng tên"]], "Phần bổ sung đã phân chia");

add("Hiệp", 3, "Điều phối upload nâng cao", [["H05", "Cancel một file đang upload"], ["H06", "File khác vẫn chạy khi một file bị hủy"], ["H07", "Retry file Error"], ["H08", "Cancel giải phóng slot"], ["H09", "Retry không tạo upload trùng"], ["H10", "Không vượt quá 3 upload đồng thời"]]);
add("Tuấn", 3, "Protocol v3", [["T05", "CANCEL hợp lệ"], ["T06", "RETRY hợp lệ"], ["T07", "RequestId sai"], ["T08", "ProtocolVersion sai"], ["T09", "ErrorCode đúng"], ["T10", "Client Server dùng đúng cùng protocol"]]);
add("Kiệt", 3, "Server quản lý upload session", [["K05", "Timeout giữa upload"], ["K06", "Client ngắt bất thường"], ["K07", "Một session lỗi nhưng session khác vẫn chạy"], ["K08", "Server tiếp tục accept"], ["K09", "Socket và stream được cleanup"]]);
add("Linh", 3, "Chunk transfer và integrity", [["L06", "File lớn"], ["L07", "Số byte gửi nhận đúng"], ["L08", "Ngắt giữa upload"], ["L09", "Checksum đúng"], ["L10", "File thiếu hoặc checksum sai bị phát hiện"]]);
add("Hiệp", 3, "Storage recovery và integration", [["BS-W3-01", "Integration test toàn luồng Client Server"]], "Phần bổ sung đã phân chia");
add("Tuấn", 3, "Storage recovery và integration", [["BS-W3-02", "Log đầy đủ vòng đời upload"]], "Phần bổ sung đã phân chia");
add("Kiệt", 3, "Storage recovery và integration", [["BS-W3-03", "Cancel cleanup file .part"], ["BS-W3-04", "Error cleanup file tạm"]], "Phần bổ sung đã phân chia");
add("Linh", 3, "Storage recovery và integration", [["BS-W3-05", "Không xóa file Completed"], ["BS-W3-06", "Tên trùng vẫn được xử lý đúng"]], "Phần bổ sung đã phân chia");

add("Hiệp", 4, "Client scheduler và final integration", [["H11", "10 file liên tục"], ["H12", "Cancel và Error gần đồng thời"], ["H13", "Không vượt quá 3 upload"], ["H14", "Không deadlock"], ["H15", "Retry không upload trùng"], ["H16", "Thống kê đúng"], ["H17", "Clean build"], ["H18", "Chạy trên 2 máy"], ["H19", "Server mất kết nối nhưng Client xử lý được"]]);
add("Tuấn", 4, "Protocol hardening và README", [["T11", "Metadata thiếu"], ["T12", "FileSize âm"], ["T13", "Metadata quá lớn"], ["T14", "RequestId thiếu"], ["T15", "ProtocolVersion sai"], ["T16", "Message bị cắt"], ["T17", "Đổi port"], ["T18", "Build và chạy theo README"], ["T19", "Client Server dùng đúng cùng protocol"]]);
add("Kiệt", 4, "Server stability và resource cleanup", [["K11", "Shutdown khi idle"], ["K12", "Shutdown khi đang upload"], ["K13", "Client disconnect"], ["K14", "Nhiều Client"], ["K15", "Server vẫn chạy sau Client lỗi"], ["K16", "Socket và stream cleanup"], ["K17", "Timeout"], ["K18", "Sai port"], ["K19", "Restart Server rồi chạy lại"]]);
add("Linh", 4, "File integrity và performance", [["L11", "Đúng số byte"], ["L12", "File thiếu"], ["L13", "File lớn"], ["L14", "RAM ổn định"], ["L15", "Throughput"], ["L16", "Benchmark mức 1"], ["L17", "Benchmark mức 2"], ["L18", "CPU và RAM"], ["L19", "File nhận mở được"], ["L20", "Integrity đúng khi có SHA-256"]]);
add("Hiệp", 4, "Stress và regression", [["BS-W4-01", "10 file x 10 MB"], ["BS-W4-02", "Một file lỗi nhưng file khác tiếp tục"], ["BS-W4-03", "Toàn bộ chức năng bắt buộc Passed"]], "Phần bổ sung đã phân chia");
add("Tuấn", 4, "Stress và regression", [["BS-W4-04", "Invalid metadata"], ["BS-W4-05", "Log đầy đủ"]], "Phần bổ sung đã phân chia");
add("Kiệt", 4, "Stress và regression", [["BS-W4-06", "File .part được cleanup"], ["BS-W4-07", "Client disconnect"], ["BS-W4-08", "Server disconnect"]], "Phần bổ sung đã phân chia");
add("Linh", 4, "Stress và regression", [["BS-W4-09", "Tên trùng _1 _2 _3"], ["BS-W4-10", "30 file x 20 MB"]], "Phần bổ sung đã phân chia");

add("Hiệp", 5, "Điều khiển hàng loạt phía Client", [["W5-H01", "Hủy tất cả file Waiting và Uploading"], ["W5-H02", "Hủy tất cả không tác động file Completed"], ["W5-H03", "Thử lại tất cả file Error và Cancelled"], ["W5-H04", "Thử lại hàng loạt không upload trùng"], ["W5-H05", "Hủy và thử lại hàng loạt không deadlock"]]);
add("Tuấn", 5, "Protocol trả tên file Server", [["W5-T01", "Completed có SavedFileName"], ["W5-T02", "ERROR không bắt buộc SavedFileName"], ["W5-T03", "Serialize và deserialize SavedFileName"], ["W5-T04", "Client Server không lệch JSON"], ["W5-T05", "README mô tả đúng trường SavedFileName"]]);
add("Kiệt", 5, "Server trả kết quả lưu file", [["W5-K01", "Server trả report_1.pdf khi trùng tên"], ["W5-K02", "Server không lộ đường dẫn tuyệt đối"], ["W5-K03", "File sai hash không được báo Completed"], ["W5-K04", "Hủy hàng loạt không còn file .part"], ["W5-K05", "Mất kết nối không còn file .part"]]);
add("Linh", 5, "Xóa mục hoàn tất và đồng bộ thống kê", [["W5-L01", "Xóa mục Completed khỏi danh sách"], ["W5-L02", "Xóa lịch sử không xóa file Server"], ["W5-L03", "Xóa Completed không ảnh hưởng file đang chạy"], ["W5-L04", "Thống kê được tính lại sau khi xóa"], ["W5-L05", "Size và hash vẫn đúng sau thao tác xóa"]]);

// Nhựt phụ trách giao diện Client trong cả năm tuần.
add("Nhựt", 1, "Client WPF cơ bản và chọn file", [["N01", "Mở cửa sổ Client và nhập IP, port"], ["N02", "Chọn file bằng hộp thoại"], ["N03", "Kéo thả file vào cửa sổ"], ["N04", "Hiển thị đúng tên, đường dẫn và kích thước file"]]);
add("Nhựt", 2, "Client WPF nhiều file, trạng thái và tiến độ", [["N05", "Chọn nhiều file và hiển thị mỗi file một dòng"], ["N06", "Kéo thả nhiều file"], ["N07", "Hiển thị Waiting, Uploading, Completed và Error"], ["N08", "Progress và tốc độ cập nhật riêng từng file"], ["N09", "Không thêm hai lần cùng đường dẫn"], ["N10", "Giao diện vẫn phản hồi khi đang upload"]]);
add("Nhựt", 3, "Client WPF Cancel, Retry và trạng thái lỗi", [["N11", "Hủy một file đang upload từ giao diện"], ["N12", "Thử lại file Error hoặc Cancelled"], ["N13", "Nút Cancel và Retry bật đúng trạng thái"], ["N14", "Progress file khác tiếp tục khi hủy một file"], ["N15", "Hiển thị lỗi riêng cho từng file"], ["N16", "Giao diện không treo khi nhiều file chạy"]]);
add("Nhựt", 4, "Client GUI Finalization và Demo UI", [["N17", "Hiển thị Connected, Disconnected và Error"], ["N18", "Hiển thị tên file dài đầy đủ hoặc có tooltip"], ["N19", "Kéo thả liên tục 20 file"], ["N20", "Tốc độ hiển thị đúng KB/s và MB/s"], ["N21", "Thống kê phiên upload khớp kết quả"], ["N22", "Thông báo tiếng Việt và ảnh chụp đúng trạng thái"]]);
add("Nhựt", 5, "Giao diện thao tác hàng loạt", [["N23", "Nút Hủy tất cả chỉ bật khi có file Waiting hoặc Uploading"], ["N24", "Nút Thử lại tất cả chỉ bật khi có file Error hoặc Cancelled"], ["N25", "Nút Xóa mục hoàn tất chỉ xóa lịch sử trên giao diện"], ["N26", "Hiển thị tên gốc và SavedFileName thực tế"], ["N27", "Danh sách cập nhật an toàn khi thao tác mạng"]]);

function xml(s) { return String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;"); }
function wrapWords(text, maxChars) {
  const words = text.split(/\s+/); const lines = []; let line = "";
  for (const word of words) {
    if ((line + " " + word).trim().length > maxChars) { if (line) lines.push(line); line = word; }
    else line = (line + " " + word).trim();
  }
  if (line) lines.push(line); return lines;
}
const assignments = {
  1: [
    ["Nguyễn Tấn Hiệp", "Khởi tạo repository và solution", "Kiểm thử gửi một file hợp lệ qua TCP"],
    ["Phạm Anh Tuấn", "Shared protocol phiên bản 1", "Kiểm thử Client khi Server chưa bật hoặc sai port"],
    ["Huỳnh Anh Kiệt", "Server TCP và xử lý kết nối", "Kiểm thử thứ tự phản hồi READY và COMPLETED"],
    ["Võ Nhật Linh", "Server lưu file, log và kiểm thử tích hợp", "Kiểm thử truyền theo chunk và mức dùng RAM"],
    ["Lê Văn Nhựt", "Client WPF cơ bản và chọn file", "Kiểm tra giao diện, chọn file và kéo thả"],
  ],
  2: [
    ["Nguyễn Tấn Hiệp", "Điều phối upload nhiều file phía Client", "Upload file mới; upload 5 file liên tiếp"],
    ["Phạm Anh Tuấn", "Protocol upload v2 và kiểm tra dữ liệu", "Nhiều Client upload cùng lúc; kiểm tra log"],
    ["Huỳnh Anh Kiệt", "Server nhận nhiều kết nối", "Upload bị ngắt và xử lý file tạm"],
    ["Võ Nhật Linh", "Truyền file theo chunk và tính toàn vẹn", "Xử lý file trùng tên"],
    ["Lê Văn Nhựt", "Client WPF nhiều file và tiến độ", "Kiểm tra trạng thái riêng từng file và giao diện không treo"],
  ],
  3: [
    ["Nguyễn Tấn Hiệp", "Cancel, Retry, Queue và lỗi độc lập", "Integration test toàn luồng Client Server"],
    ["Phạm Anh Tuấn", "Protocol v3 và validation", "Kiểm tra log đầy đủ vòng đời upload"],
    ["Huỳnh Anh Kiệt", "Server session, timeout và cleanup", "Cleanup .part khi Cancel và Error"],
    ["Võ Nhật Linh", "Chunk transfer và integrity", "Không xóa file Completed; xử lý tên trùng"],
    ["Lê Văn Nhựt", "Client GUI Cancel và Retry", "Kiểm tra nút theo trạng thái và progress đồng thời"],
  ],
  4: [
    ["Nguyễn Tấn Hiệp", "Client scheduler và final integration", "Stress 10 file x 10 MB; lỗi độc lập; regression bắt buộc"],
    ["Phạm Anh Tuấn", "Protocol hardening và README", "Invalid metadata; kiểm tra log đầy đủ"],
    ["Huỳnh Anh Kiệt", "Server stability và resource cleanup", "Cleanup .part; Client và Server disconnect"],
    ["Võ Nhật Linh", "File integrity và performance", "Tên trùng _1 _2 _3; stress 30 file x 20 MB"],
    ["Lê Văn Nhựt", "Client GUI Finalization và Demo UI", "Kết nối, tên dài, tốc độ, thống kê và ảnh demo"],
  ],
  5: [
    ["Nguyễn Tấn Hiệp", "Hủy tất cả và thử lại tất cả", "Kiểm tra giới hạn 3 upload và không deadlock"],
    ["Phạm Anh Tuấn", "Protocol trả SavedFileName", "Kiểm tra JSON và README"],
    ["Huỳnh Anh Kiệt", "Server trả tên file thực tế", "Kiểm tra hash, đường dẫn và cleanup .part"],
    ["Võ Nhật Linh", "Xóa mục hoàn tất và đồng bộ thống kê", "Kiểm tra không xóa file Server và không ảnh hưởng queue"],
    ["Lê Văn Nhựt", "Hoàn thiện giao diện thao tác hàng loạt", "Kiểm tra nút, SavedFileName và cập nhật danh sách"],
  ],
};

for (let week = 1; week <= 5; week++) {
  const width = 1200, height = 820;
  let rows = "";
  assignments[week].forEach((r, i) => {
    const y = 150 + i * 118;
    const fill = i % 2 ? "#F7F9FC" : "#EEF3F8";
    rows += `<rect x="50" y="${y}" width="1100" height="105" rx="8" fill="${fill}" stroke="#CAD3DF"/>`;
    const cols = [r[0], r[1], r[2]]; const xs = [75, 300, 735]; const max = [21, 36, 31];
    cols.forEach((text, c) => {
      const lines = wrapWords(text, max[c]);
      rows += lines.map((line, j) => `<text x="${xs[c]}" y="${y + 36 + j * 22}" font-size="18" fill="#1F2937" font-family="DejaVu Sans, Arial" font-weight="${c===0?600:400}">${xml(line)}</text>`).join("");
    });
  });
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}">
    <rect width="100%" height="100%" fill="#FFFFFF"/>
    <text x="50" y="48" font-size="30" font-family="DejaVu Sans, Arial" font-weight="700" fill="#17365D">Bảng phân công kiểm thử Tuần ${week}</text>
    <text x="50" y="78" font-size="16" font-family="DejaVu Sans, Arial" fill="#4B5563">Phân công kiểm thử cho năm thành viên</text>
    <rect x="50" y="100" width="1100" height="38" fill="#2F5597"/>
    <text x="75" y="125" font-size="16" font-family="DejaVu Sans, Arial" font-weight="700" fill="#FFFFFF">Thành viên</text>
    <text x="300" y="125" font-size="16" font-family="DejaVu Sans, Arial" font-weight="700" fill="#FFFFFF">Phần phụ trách chính</text>
    <text x="735" y="125" font-size="16" font-family="DejaVu Sans, Arial" font-weight="700" fill="#FFFFFF">Kiểm thử bổ sung</text>
    ${rows}
    <text x="50" y="770" font-size="14" font-family="DejaVu Sans, Arial" fill="#6B7280">Nguồn: kế hoạch dự án Tuần ${week}; phân công kiểm thử theo từng thành viên.</text>
  </svg>`;
  await sharp(Buffer.from(svg)).png().toFile(path.join(evidenceDir, `phan-cong-tuan-${week}.png`));
}

const wb = Workbook.create();
const navy = "#1F4E78", blue = "#5B9BD5", line = "#D9E1F2", pale = "#F7F9FC";
const font = "Arial";

function titleBlock(sheet, title, subtitle, endCol) {
  sheet.showGridLines = false;
  sheet.mergeCells(`A1:${endCol}1`);
  sheet.getRange("A1").values = [[title]];
  sheet.getRange("A1").format = { font: { name: font, size: 16, bold: true, color: "#000000" }, rowHeight: 28, verticalAlignment: "center" };
  sheet.mergeCells(`A2:${endCol}2`);
  sheet.getRange("A2").values = [[subtitle]];
  sheet.getRange("A2").format = { font: { name: font, size: 10, italic: true, color: "#555555" }, rowHeight: 22, verticalAlignment: "center" };
}

const summary = wb.worksheets.add("Danh sách Test_Case");
titleBlock(summary, "Danh sách test case dự án truyền file TCP", "Gồm 5 thành viên thực hiện kiểm thử. Kết quả và minh chứng để trống để nhóm cập nhật sau khi chạy.", "H");
summary.getRange("A4:H4").values = [["STT", "Mã test case", "Thành viên", "Tuần", "Chức năng", "Nội dung kiểm thử", "Loại phân công", "Trạng thái"]];
summary.getRange("A5").write(cases.map((c, i) => [i + 1, c.id, c.owner, c.week, c.feature, c.title, c.kind, "NOT TEST YET"]));
summary.freezePanes.freezeRows(4);
summary.getRange(`A4:H${cases.length + 4}`).format.font = { name: font, size: 10, color: "#1F1F1F" };
summary.getRange("A4:H4").format = { fill: navy, font: { name: font, size: 10, bold: true, color: "#FFFFFF" }, horizontalAlignment: "center", verticalAlignment: "center", wrapText: true, rowHeight: 34, borders: { preset: "all", style: "thin", color: "#FFFFFF" } };
summary.getRange(`A5:H${cases.length + 4}`).format = { verticalAlignment: "top", wrapText: true, borders: { preset: "inside", style: "thin", color: line } };
summary.getRange(`A5:A${cases.length + 4}`).format.horizontalAlignment = "center";
summary.getRange(`B5:B${cases.length + 4}`).format.horizontalAlignment = "center";
summary.getRange(`D5:D${cases.length + 4}`).format.horizontalAlignment = "center";
summary.getRange(`H5:H${cases.length + 4}`).format.horizontalAlignment = "center";
summary.getRange("A:A").format.columnWidth = 6;
summary.getRange("B:B").format.columnWidth = 15;
summary.getRange("C:C").format.columnWidth = 21;
summary.getRange("D:D").format.columnWidth = 7;
summary.getRange("E:E").format.columnWidth = 31;
summary.getRange("F:F").format.columnWidth = 40;
summary.getRange("G:G").format.columnWidth = 24;
summary.getRange("H:H").format.columnWidth = 16;
summary.getRange(`H5:H${cases.length + 4}`).dataValidation = { rule: { type: "list", values: ["NOT TEST YET", "WORK IN PROGRESS", "PASSED", "FAILED"] } };
summary.getRange(`H5:H${cases.length + 4}`).conditionalFormats.add("containsText", { text: "PASSED", format: { fill: "#C6EFCE", font: { color: "#006100" } } });
summary.getRange(`H5:H${cases.length + 4}`).conditionalFormats.add("containsText", { text: "FAILED", format: { fill: "#FFC7CE", font: { color: "#9C0006" } } });
summary.getRange(`H5:H${cases.length + 4}`).conditionalFormats.add("containsText", { text: "WORK IN PROGRESS", format: { fill: "#FFEB9C", font: { color: "#9C6500" } } });
summary.tables.add(`A4:H${cases.length + 4}`, true, "TestCaseSummary").style = "TableStyleMedium2";

for (const p of people) {
  const sh = wb.worksheets.add(p.tab);
  const mine = cases.filter(c => c.owner === p.name);
  titleBlock(sh, `Test case của ${p.name}`, `Tổng cộng ${mine.length} test case từ Tuần 1 đến Tuần 5. Phần hình ảnh/log sẽ được bổ sung sau khi chạy test.`, "J");
  const headers = ["Mã test case", "Tên màn hình / chức năng", "Mô tả", "Dữ liệu / kịch bản", "Kết quả mong đợi", "Kết quả test 1", "Kết quả test 2", "Kết quả test 3", "Trạng thái", "Minh chứng"];
  sh.getRange("A4:J4").values = [headers];
  sh.getRange("A5").write(mine.map(c => [c.id, `Tuần ${c.week} - ${c.feature}\n(${c.sourceId})`, c.description, c.scenario, c.expected, "", "", "", "NOT TEST YET", "Nhóm bổ sung ảnh/log sau"]));
  const last = mine.length + 4;
  sh.freezePanes.freezeRows(4);
  sh.freezePanes.freezeColumns(2);
  sh.getRange(`A4:J${last}`).format.font = { name: font, size: 10, color: "#1F1F1F" };
  sh.getRange("A4:J4").format = { fill: navy, font: { name: font, size: 10, bold: true, color: "#FFFFFF" }, horizontalAlignment: "center", verticalAlignment: "center", wrapText: true, rowHeight: 42, borders: { preset: "all", style: "thin", color: "#FFFFFF" } };
  sh.getRange(`A5:J${last}`).format = { verticalAlignment: "top", wrapText: true, borders: { preset: "inside", style: "thin", color: line } };
  sh.getRange(`A5:A${last}`).format.horizontalAlignment = "center";
  sh.getRange(`F5:J${last}`).format.horizontalAlignment = "center";
  sh.getRange(`A5:J${last}`).format.rowHeight = 92;
  sh.getRange("A:A").format.columnWidth = 14;
  sh.getRange("B:B").format.columnWidth = 31;
  sh.getRange("C:C").format.columnWidth = 34;
  sh.getRange("D:D").format.columnWidth = 48;
  sh.getRange("E:E").format.columnWidth = 44;
  sh.getRange("F:H").format.columnWidth = 20;
  sh.getRange("I:I").format.columnWidth = 18;
  sh.getRange("J:J").format.columnWidth = 24;
  sh.getRange(`I5:I${last}`).dataValidation = { rule: { type: "list", values: ["NOT TEST YET", "WORK IN PROGRESS", "PASSED", "FAILED"] } };
  sh.getRange(`I5:I${last}`).conditionalFormats.add("containsText", { text: "PASSED", format: { fill: "#C6EFCE", font: { color: "#006100" } } });
  sh.getRange(`I5:I${last}`).conditionalFormats.add("containsText", { text: "FAILED", format: { fill: "#FFC7CE", font: { color: "#9C0006" } } });
  sh.getRange(`I5:I${last}`).conditionalFormats.add("containsText", { text: "WORK IN PROGRESS", format: { fill: "#FFEB9C", font: { color: "#9C6500" } } });
  const tableName = `Cases${p.prefix}`;
  sh.tables.add(`A4:J${last}`, true, tableName).style = "TableStyleMedium2";
  sh.tabColor = p.color;
}

const ev = wb.worksheets.add("Minh chứng phân công");
titleBlock(ev, "Minh chứng phân công kiểm thử 5 tuần", "Năm hình dưới đây thể hiện phân công cho năm thành viên trong file test case.", "P");
ev.getRange("A:A").format.columnWidth = 3;
for (let c = 1; c < 16; c++) ev.getRangeByIndexes(0, c, 1, 1).format.columnWidth = 11;
for (let week = 1; week <= 5; week++) {
  const startRow = 4 + (week - 1) * 43;
  ev.mergeCells(`B${startRow}:P${startRow}`);
  ev.getRange(`B${startRow}`).values = [[`Hình ${week}  Phân công kiểm thử Tuần ${week}`]];
  ev.getRange(`B${startRow}`).format = { font: { name: font, size: 12, bold: true, color: "#000000" }, rowHeight: 24 };
  const png = await fs.readFile(path.join(evidenceDir, `phan-cong-tuan-${week}.png`));
  ev.images.add({ dataUrl: `data:image/png;base64,${png.toString("base64")}`, anchor: { from: { row: startRow, col: 1 }, extent: { widthPx: 760, heightPx: 520 } } });
}
ev.tabColor = "#A5A5A5";

const summaryCheck = await wb.inspect({ kind: "table", range: `Danh sách Test_Case!A1:H12`, include: "values,formulas", tableMaxRows: 12, tableMaxCols: 8, maxChars: 5000 });
console.log(summaryCheck.ndjson);
const errorCheck = await wb.inspect({ kind: "match", searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!", options: { useRegex: true, maxResults: 100 }, summary: "final formula error scan" });
console.log(errorCheck.ndjson);

for (const name of ["Danh sách Test_Case", ...people.map(p => p.tab), "Minh chứng phân công"]) {
  const preview = await wb.render({ sheetName: name, autoCrop: "all", scale: name === "Minh chứng phân công" ? 0.55 : 0.7, format: "png" });
  await fs.writeFile(path.join(root, `tmp/xlsx_build/preview-${name.replace(/[^A-Za-z0-9À-ỹ]+/g, "-")}.png`), new Uint8Array(await preview.arrayBuffer()));
}

const out = await SpreadsheetFile.exportXlsx(wb);
const outputPath = path.join(outputDir, "Test_Case_LTM_Project_5_Tuan.xlsx");
await out.save(outputPath);
console.log(JSON.stringify({ outputPath, caseCount: cases.length, byOwner: Object.fromEntries(people.map(p => [p.name, cases.filter(c => c.owner === p.name).length])) }));
