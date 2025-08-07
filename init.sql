BEGIN;
CREATE TABLE public.version
(
    version serial NOT NULL,
    created_at timestamp without time zone DEFAULT now(),
    CONSTRAINT version_pkey PRIMARY KEY (version)
);
INSERT INTO Version (version) VALUES (1);
COMMIT;
