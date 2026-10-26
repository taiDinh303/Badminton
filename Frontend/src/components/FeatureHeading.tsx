import type { LucideIcon } from "lucide-react";
import type { ReactNode } from "react";

export default function FeatureHeading({ icon: Icon, title, subtitle, actions }: {
  icon: LucideIcon;
  title: string;
  subtitle?: string;
  actions?: ReactNode;
}) {
  return <div className="feature-heading">
    <div className="feature-heading-copy">
      <span className="feature-icon"><Icon size={22} aria-hidden="true" /></span>
      <div><h1>{title}</h1>{subtitle && <p>{subtitle}</p>}</div>
    </div>
    {actions && <div className="feature-heading-actions">{actions}</div>}
  </div>;
}
