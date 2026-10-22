import { RouterProvider } from "react-router-dom";
import Providers from "./app/Providers";
import { router, RouteBoundary } from "./app/routes";

export default function App() {
  return <Providers><RouteBoundary><RouterProvider router={router} /></RouteBoundary></Providers>;
}
