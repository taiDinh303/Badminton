import { useQuery } from "@tanstack/react-query";
import { api } from "../../../services/api";
import { SelectField, Callout } from "../../../components/ui";

export default function BranchPicker({
  value,
  onChange,
  all = true,
}: {
  value: number;
  onChange: (value: number) => void;
  all?: boolean;
}) {
  const branches = useQuery({
    queryKey: ["branches"],
    queryFn: api.branches.list,
  });
  return (
    <>
      <SelectField
        label="Cơ sở"
        value={value}
        onChange={(e) => onChange(Number(e.target.value))}
      >
        <option value={0}>
          {all ? "Theo phạm vi được phép" : "Chọn cơ sở"}
        </option>
        {branches.data?.map((b) => (
          <option key={b.id} value={b.id}>
            {b.name}
          </option>
        ))}
      </SelectField>
      {branches.error && (
        <Callout tone="error">{branches.error.message}</Callout>
      )}
    </>
  );
}
