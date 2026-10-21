import { useEffect, useRef, useState } from "react";
import { Button, Callout } from "../../../components/ui";

type Detector = { detect: (source: HTMLVideoElement) => Promise<{ rawValue: string }[]> };
type DetectorConstructor = new (options: { formats: string[] }) => Detector;

export default function TokenScanner({ onToken }: { onToken: (token: string) => void }) {
  const video = useRef<HTMLVideoElement>(null);
  const [active, setActive] = useState(false);
  const [error, setError] = useState("");
  const callback = useRef(onToken);
  useEffect(() => { callback.current = onToken; }, [onToken]);
  useEffect(() => {
    if (!active) return;
    let disposed = false;
    let stream: MediaStream | undefined;
    let timer: ReturnType<typeof setTimeout> | undefined;
    async function start() {
      try {
        const BarcodeDetector = (window as unknown as { BarcodeDetector?: DetectorConstructor }).BarcodeDetector;
        if (!BarcodeDetector) throw new Error("Trình duyệt chưa hỗ trợ quét camera. Dùng máy quét hoặc dán token bên dưới.");
        const detector = new BarcodeDetector({ formats: ["qr_code"] });
        stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: { ideal: "environment" } }, audio: false });
        if (disposed) { stream.getTracks().forEach((t) => t.stop()); return; }
        video.current!.srcObject = stream;
        await video.current!.play();
        async function scan() {
          if (disposed || !video.current) return;
          try {
            const values = await detector.detect(video.current);
            const token = values[0]?.rawValue?.trim();
            if (token) { callback.current(token); setActive(false); return; }
          } catch { /* Wait for a frame that can be decoded. */ }
          if (!disposed) timer = setTimeout(scan, 200);
        }
        await scan();
      } catch (e) { if (!disposed) { setError(e instanceof Error ? e.message : "Không mở được camera."); setActive(false); } }
    }
    void start();
    return () => { disposed = true; clearTimeout(timer); stream?.getTracks().forEach((t) => t.stop()); };
  }, [active]);
  return <div className="mb-3">
    <Button variant="ghost" size="sm" onClick={() => { setError(""); setActive((v) => !v); }}>{active ? "Dừng camera" : "Quét token bằng camera"}</Button>
    {active && <video ref={video} muted playsInline className="mt-2 w-full rounded-lg" aria-label="Camera quét token" />}
    {error && <Callout tone="warning" className="mt-2">{error}</Callout>}
  </div>;
}
