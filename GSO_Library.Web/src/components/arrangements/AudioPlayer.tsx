import { useEffect, useRef, useState } from 'react';
import { Spinner } from 'react-bootstrap';
import { arrangementsApi } from '../../api/arrangements';
import type { ArrangementFile } from '../../types';

interface AudioPlayerProps {
  arrangementId: number;
  file: ArrangementFile;
}

export default function AudioPlayer({ arrangementId, file }: AudioPlayerProps) {
  const [blobUrl, setBlobUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(false);
  const blobUrlRef = useRef<string | null>(null);

  useEffect(() => {
    return () => {
      if (blobUrlRef.current) {
        URL.revokeObjectURL(blobUrlRef.current);
      }
    };
  }, []);

  const handlePlay = async () => {
    if (blobUrl || loading) return;
    setLoading(true);
    setError(false);
    try {
      const response = await arrangementsApi.downloadFile(arrangementId, file.id);
      const url = URL.createObjectURL(response.data as Blob);
      blobUrlRef.current = url;
      setBlobUrl(url);
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  };

  if (error) {
    return <small className="text-danger">Failed to load audio</small>;
  }

  if (blobUrl) {
    return <audio controls src={blobUrl} className="w-100 mt-1" style={{ height: 36 }} />;
  }

  return (
    <button
      type="button"
      className="btn btn-sm btn-outline-secondary mt-1"
      onClick={handlePlay}
      disabled={loading}
    >
      {loading ? <><Spinner animation="border" size="sm" className="me-1" />Loading...</> : '▶ Play'}
    </button>
  );
}
