# Cluster-wide add-ons installed via Helm: ingress-nginx (bound to the static
# public IP) and cert-manager (+ Let's Encrypt HTTP-01 ClusterIssuers).

resource "helm_release" "ingress_nginx" {
  name             = "ingress-nginx"
  repository       = "https://kubernetes.github.io/ingress-nginx"
  chart            = "ingress-nginx"
  version          = var.ingress_nginx_chart_version
  namespace        = "ingress-nginx"
  create_namespace = true

  # Bind the Azure LB to the pre-allocated static IP that lives in the network RG.
  set = [
    {
      name  = "controller.service.loadBalancerIP"
      value = var.public_ip_address
    },
    {
      name  = "controller.service.annotations.service\\.beta\\.kubernetes\\.io/azure-load-balancer-resource-group"
      value = var.public_ip_resource_group
    },
    {
      name  = "controller.ingressClassResource.name"
      value = var.ingress_class_name
    },
    # Preserve the client source IP.
    {
      name  = "controller.service.externalTrafficPolicy"
      value = "Local"
    },
  ]
}

resource "helm_release" "cert_manager" {
  name             = "cert-manager"
  repository       = "https://charts.jetstack.io"
  chart            = "cert-manager"
  version          = var.cert_manager_chart_version
  namespace        = "cert-manager"
  create_namespace = true

  set = [
    {
      name  = "crds.enabled"
      value = "true"
    },
  ]
}

# Let's Encrypt issuers (staging for validation, prod for real certs), HTTP-01 via
# the ingress class. NOTE: these require cert-manager's CRDs to exist first — on a
# fresh cluster apply cert-manager before these (two-phase apply, see infra/README).
resource "kubernetes_manifest" "cluster_issuer_staging" {
  manifest = {
    apiVersion = "cert-manager.io/v1"
    kind       = "ClusterIssuer"
    metadata   = { name = "letsencrypt-staging" }
    spec = {
      acme = {
        server              = "https://acme-staging-v02.api.letsencrypt.org/directory"
        email               = var.acme_email
        privateKeySecretRef = { name = "letsencrypt-staging-account-key" }
        solvers = [{
          http01 = { ingress = { class = var.ingress_class_name } }
        }]
      }
    }
  }

  depends_on = [helm_release.cert_manager]
}

resource "kubernetes_manifest" "cluster_issuer_prod" {
  manifest = {
    apiVersion = "cert-manager.io/v1"
    kind       = "ClusterIssuer"
    metadata   = { name = "letsencrypt-prod" }
    spec = {
      acme = {
        server              = "https://acme-v02.api.letsencrypt.org/directory"
        email               = var.acme_email
        privateKeySecretRef = { name = "letsencrypt-prod-account-key" }
        solvers = [{
          http01 = { ingress = { class = var.ingress_class_name } }
        }]
      }
    }
  }

  depends_on = [helm_release.cert_manager]
}
