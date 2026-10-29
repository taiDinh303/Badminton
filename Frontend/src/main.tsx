import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import "./theme.css";
import "./index.css";
import "./mobile.css";
import App from "./App";
import "./redesign.css";
import "./workspace.css";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
