import { useEffect } from 'react';

const THRESHOLD = 80;
const MAX_SPEED = 12;

export function useDragAutoScroll() {
  useEffect(() => {
    let rafId: number | null = null;
    let speed = 0;

    const tick = () => {
      if (speed !== 0) {
        window.scrollBy(0, speed);
        rafId = requestAnimationFrame(tick);
      } else {
        rafId = null;
      }
    };

    const onDragOver = (e: DragEvent) => {
      const vh = window.innerHeight;
      const y = e.clientY;
      if (y < THRESHOLD) {
        speed = -MAX_SPEED * (1 - y / THRESHOLD);
      } else if (y > vh - THRESHOLD) {
        speed = MAX_SPEED * (1 - (vh - y) / THRESHOLD);
      } else {
        speed = 0;
      }
      if (speed !== 0 && rafId === null) rafId = requestAnimationFrame(tick);
    };

    const stop = () => {
      speed = 0;
      if (rafId !== null) { cancelAnimationFrame(rafId); rafId = null; }
    };

    document.addEventListener('dragover', onDragOver);
    document.addEventListener('dragend', stop);
    document.addEventListener('drop', stop);
    return () => {
      document.removeEventListener('dragover', onDragOver);
      document.removeEventListener('dragend', stop);
      document.removeEventListener('drop', stop);
      stop();
    };
  }, []);
}
