import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { api } from "../../../services/api";
import { experience } from "../../../services/api/experience";
import { Button, Callout, PageHeader } from "../../../components/ui";
import { useExperienceAction } from "../hooks/useExperienceAction";
import { ActionStatus, Panel } from "../components/ExperienceUi";

export default function FavoritesPage() {
  const action = useExperienceAction();
  const favorites = useQuery({
    queryKey: ["experience", "favorites"],
    queryFn: experience.favorites,
  });
  const courts = useQuery({
    queryKey: ["courts"],
    queryFn: () => api.courts.list(),
  });
  return (
    <div className="mx-auto max-w-3xl p-4 md:p-6">
      <PageHeader
        title="Sân yêu thích"
        subtitle="Lưu sân thường chơi để chọn nhanh lần sau"
      />
      {favorites.error && (
        <Callout tone="error">{favorites.error.message}</Callout>
      )}
      {courts.error && <Callout tone="error">{courts.error.message}</Callout>}
      <ActionStatus action={action} />
      {courts.data
        ?.filter((c) => c.isActive)
        .map((c) => {
          const saved =
            favorites.data?.some((f) => f.courtId === c.id) ?? false;
          return (
            <Panel key={c.id} title={c.name}>
              <div className="flex flex-wrap gap-3">
                <Button
                  variant={saved ? "primary" : "ghost"}
                  aria-pressed={saved}
                  disabled={action.isPending || !favorites.data}
                  onClick={() =>
                    action.mutate(() => experience.favorite(c.id, !saved))
                  }
                >
                  {saved ? "Bỏ yêu thích" : "Yêu thích"}
                </Button>
                <Link
                  className="self-center text-primary underline"
                  to={`/book?branchId=${c.branchId}`}
                >
                  Đặt tại cơ sở này
                </Link>
              </div>
            </Panel>
          );
        })}
    </div>
  );
}
