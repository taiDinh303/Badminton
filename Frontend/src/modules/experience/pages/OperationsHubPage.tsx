import { Link } from "react-router-dom";
import { Card, PageHeader } from "../../../components/ui";
import { useAuth } from "../../../store/auth";

export default function OperationsHubPage() {
  const admin = useAuth((s) => s.user?.role === "Admin");
  const links = [
    [
      "calendar",
      "Lịch ngày/tuần & đặt hộ",
      "Theo dõi sân, khách đến và đặt tại quầy.",
    ],
    [
      "cases",
      "Phản ánh & khiếu nại",
      "Xác minh hiện diện, xử lý yêu cầu của khách.",
    ],
    [
      "incidents",
      "Sự cố & liên hệ khách",
      "Thông báo đúng đơn bị ảnh hưởng, theo dõi kết quả liên hệ.",
    ],
    [
      "staff-work",
      "Ca trực & bàn giao",
      "Lập ca, theo dõi việc tồn giữa các ca.",
    ],
    [
      "deliveries",
      "Trạng thái gửi thông báo",
      "Theo dõi kênh gửi thất bại, thử lại có kiểm soát.",
    ],
    [
      "moderation",
      "Kiểm duyệt đánh giá",
      "Xử lý báo cáo, ẩn hoặc khôi phục có lý do.",
    ],
    [
      "insights",
      "Khách hàng & xuất báo cáo",
      "Khách mới/quay lại, no-show, hàng chờ và CSV.",
    ],
    ...(admin
      ? [
          [
            "privacy",
            "Yêu cầu dữ liệu cá nhân",
            "Xác minh và phản hồi yêu cầu sửa/xóa.",
          ],
        ]
      : []),
  ];
  return (
    <div className="mx-auto max-w-5xl p-4 md:p-6">
      <PageHeader
        title="Công cụ vận hành"
        subtitle="Từng thao tác được kiểm quyền theo cơ sở tại server"
      />
      <div className="mt-5 grid gap-4 sm:grid-cols-2">
        {links.map(([path, title, text]) => (
          <Card key={path} className="p-4">
            <Link
              className="font-bold text-primary underline"
              to={`/admin/operations/${path}`}
            >
              {title}
            </Link>
            <p className="mt-2 text-sm text-muted">{text}</p>
          </Card>
        ))}
      </div>
    </div>
  );
}
